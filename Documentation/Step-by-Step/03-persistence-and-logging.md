# 03 — Persistence, Logging, and Starting Contracts

**Date:** 2026-09-09
**Status:** Applications module persistence complete and applied to LocalDB.
Serilog wired up. Contracts layer just started (interface + DTO designed,
implementation not yet written).

## Persistence: what got built

- `Infrastructure/ApplicationDbContext.cs` — `internal sealed class`,
  `DbSet<Domain.Application> Applications` only (no separate `DbSet`s for
  `ApplicationProject`/`ApplicationLink` — they're only ever reached
  through `Application.Projects`/`Application.Links`, by design).
- `modelBuilder.HasDefaultSchema("applications")` — the actual mechanism
  behind the schema-per-module decision from day one.
- Two required-relationship configurations (`HasMany().WithOne()
  .HasForeignKey("ApplicationId").IsRequired()` for both `Projects` and
  `Links`) — needed because EF Core's default convention makes a shadow FK
  *optional* when there's no reference navigation back to the parent, and
  neither `ApplicationProject` nor `ApplicationLink` means anything without
  an owning `Application`.
- First migration (`InitialCreate`) generated and applied to SQL Server
  LocalDB (database `ApplicationCatalog`).

## Key lesson: convention vs. explicit configuration in EF Core

Worth generalizing (added to the Playbook): most of what looked like
necessary `OnModelCreating` configuration wasn't doing anything. EF Core
reads your C# types directly — nullable reference type annotations decide
column nullability, a property named `Id` is the primary key, `List<string>`
becomes a JSON column, a collection navigation property implies a
one-to-many relationship with an auto-created shadow FK. A whole block of
`entity.Property(e => e.X)` calls with nothing chained after them were
pure no-ops, no different from not writing them at all. **Only write
`OnModelCreating` config for the two cases where you're overriding
convention's default (required relationships were the case here) or
telling EF Core something it has no way to infer on its own** (custom
column names/types, max lengths, etc.) — not defensively, "just in case."

## Module registration pattern: public extension method wrapping an internal DbContext

`ApplicationDbContext` is `internal`, but `Program.cs` in the Web host
(a different assembly) needs to register it. The fix, and the pattern for
every future module:

```csharp
public static class ApplicationsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
```

This class is `public`, at the module's project root (not nested under
`Domain`/`Infrastructure`/`Contracts`). It can reference the `internal`
`ApplicationDbContext` because `internal` only blocks *other* assemblies —
code in the same project can always see it. The host calls
`AddApplicationsModule(connectionString)` and never needs to know
`ApplicationDbContext` exists at all.

## EF Core CLI tooling gotcha: package placement

For the "DbContext in a class library, ASP.NET Core app as startup
project" split (`--project <module>` / `--startup-project <web host>`):

- `Microsoft.EntityFrameworkCore.SqlServer` only needs to go on the
  **module** (target) project — it flows to the host transitively through
  the project reference, since the host never calls `UseSqlServer` itself.
- `Microsoft.EntityFrameworkCore.Design` needs to go on **both** — the
  module (for the target side) *and* the startup project (the Web host),
  because the tooling literally builds the startup project to discover the
  DbContext through its service registrations, and needs Design-time
  services available there specifically. Missing it on the host produces:
  `Your startup project 'X' doesn't reference Microsoft.EntityFrameworkCore.Design.`

## Migration mishap: regenerating a migration you already applied

Removed and regenerated `InitialCreate` locally (to add the required-FK
fix) *after* it had already been applied to LocalDB once. `migrations
remove` only deletes local files — it doesn't touch a database that
already has the tables. Result: `database update` failed with
`There is already an object named 'Applications' in the database`,
because the regenerated migration had a new ID the database's
`__EFMigrationsHistory` didn't recognize, so it tried to create
already-existing tables again. Fixed with `dotnet ef database drop` +
`dotnet ef database update` — **only safe because this was a local dev
database with no real data.** Once real data exists, this is not the
move; you'd write a proper corrective migration instead.

## Serilog: two-stage initialization

Verified against Serilog's own current docs (things move between
versions, worth checking rather than assuming). Bootstrap logger exists
from the very first line of `Program.cs`, before configuration/DI exist,
specifically to catch startup failures that happen before a "real" logger
could be built:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());
    // ... rest of Program.cs ...
    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
```

Packages: `Serilog.AspNetCore`, `Serilog.Settings.Configuration` (to read
the `Serilog` section from `appsettings.json`), `Serilog.Sinks.Console`.

**Real bugs hit and fixed along the way, not just theory:**
- `appsettings.json` broken twice by hand-editing JSON (an extra
  unkeyed `{` once, a missing closing `}` another time) — both surfaced as
  `WebApplication.CreateBuilder(args)` throwing at startup, which pointed
  at `Program.cs` in the stack trace even though the actual mistake was in
  a completely different file. Lesson logged in the Playbook: run a
  formatter on hand-edited JSON before moving on, and remember an
  exception's location isn't always where the real mistake lives.
- `AddSerilog(...)` forgotten entirely for two review rounds — the app
  compiled and ran, but `app.UseSerilogRequestLogging()` threw
  `Unable to resolve service for type 'DiagnosticContext'` because that
  middleware depends on services only `AddSerilog` registers.

## The `HostAbortedException` / Fatal-log noise, and why it mattered

Every `dotnet ef` command that needs the `DbContext` (`migrations add`,
`migrations list`, `database update`, `database drop`) builds the app's
host to pull it from DI, then deliberately throws
`Microsoft.Extensions.Hosting.HostAbortedException` to stop before
actually running — confirmed against Microsoft's own docs ("This
exception should not be thrown or handled by user code") and a detailed
community writeup of the exact `DiagnosticListener`/`"HostBuilt"` event
mechanism involved. Because `Program.cs`'s own `try/catch` around `Main`
caught it like any other exception, every single migration command logged
a scary `Log.Fatal` block, even though the command succeeded.

Fixed with an exception filter so it was never actually caught, not
caught-and-rethrown:
```csharp
catch (Exception ex) when (ex is not HostAbortedException)
```
**Why this was worth fixing, not just cosmetic:** if Fatal-level logs ever
trigger alerts/notifications, routine Fatal noise trains people to ignore
or mute Fatal alerts — exactly the failure mode you don't want for the one
time something is genuinely fatal. (This specific exception can only ever
fire under design-time tooling, never in a real running deployment — but
the general principle, that a log severity has to mean what it claims or
it stops being trustworthy, applies far beyond this one case.)

## Starting Contracts

First public surface for the Applications module, designed but not yet
implemented:
```csharp
public interface IApplicationsModuleApi
{
    Task<IReadOnlyList<ApplicationSummary>> GetApplicationsAsync();
}

public sealed class ApplicationSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ApplicationStatus Status { get; init; }
    public string? BusinessOwner { get; init; }
    public string? ItOwner { get; init; }
}
```
`ApplicationSummary` is a **summary shape** for a listing page — not the
full `Application` with its `Projects`/`Links` collections. It pragmatically
reuses `Domain.ApplicationStatus` directly rather than duplicating an
identical enum into `Contracts` — a plain enum with no behavior isn't worth
the ceremony of a second copy, same reasoning as not splitting
Domain/Infrastructure into separate projects. That means `ApplicationStatus`
stays `public` even once the rest of `Domain/` becomes `internal` (next
step) — not everything under `Domain/` needs hiding, only what's actually
worth hiding.

## Still open

- `Domain/*` types are still `public` — flip to `internal` once the
  `Contracts` implementation exists to replace direct access (in progress).
- `IApplicationsModuleApi` implementation class not yet written.
- MVC controller/view over `Application` not yet started.
