# Modular Monolith Playbook (.NET)

A project-agnostic distillation of the patterns used in this repo, meant to
seed the *next* modular-monolith project rather than re-deriving these
decisions from scratch each time. Where this repo made a domain-specific
choice, that lives in `Step-by-Step/`, not here.

## Naming convention

```
<SolutionName>.sln
src/
  Modules/
    <ModuleName>/<SolutionName>.Modules.<ModuleName>/
  Host/
    <SolutionName>.<HostKind>/        (e.g. .Web for MVC, .Api for a JSON API)
tests/
  <SolutionName>.Modules.<ModuleName>.Tests/
```

## Module boundary rule

- **One class library project per module.** Don't split a module into
  separate Domain/Application/Infrastructure assemblies — that's ceremony
  without payoff until a module is genuinely large.
- Inside a module project, organize by folder:
  - `Domain/` — entities, value objects. No outward dependencies.
  - `Infrastructure/` — the module's own persistence (`DbContext`, EF
    configs, repositories).
  - `Contracts/` — the module's public surface: an interface + DTOs. This
    is the **only** folder marked `public`. Everything else is `internal`.
- The boundary is enforced by C# visibility + project references: a
  referencing project can only see `public` types, so `internal` types are
  physically unreachable from outside the module. (An upgrade to consider
  later, once this stops feeling sufficient: architecture tests, e.g.
  NetArchTest, to enforce dependency direction automatically.)
- **Folders vs. separate projects for `Domain`/`Infrastructure`/`Contracts`
  isn't a function of app size.** Folders remain the right default even in
  a large, real modular monolith. Only promote a layer to its own project
  when you need something folders genuinely can't give you: a
  compiler-enforced wall (e.g. `Domain` must be provably free of EF Core
  because it's shared with a client project that can't reference it), not
  "the app/module got big." A module getting too big is much more often a
  sign it should split into *more modules* (recursing on the same judgment
  call used to carve out modules in the first place), not that its internal
  layers need separate assemblies. If you want compiler-adjacent
  enforcement without the project-per-layer ceremony, that's exactly what
  the architecture-tests upgrade below is for.

## Persistence pattern

- One shared database, **separate schema per module**
  (`<module>.<table>`). Cheaper to operate than a database per module,
  while still forbidding cross-module SQL joins — each module owns its
  schema and is the only thing that queries it directly.
- Escalate to separate databases per module only if schema-sharing
  becomes an actual operational bottleneck — not preemptively.

## Wiring a module's persistence into the host without breaking the boundary

The `DbContext` should be `internal` like the rest of `Infrastructure/` —
but the host (a different assembly) needs to register it. Don't make the
`DbContext` `public` to solve this; expose a `public` registration method
instead, at the module project's root:

```csharp
public static class XModuleServiceCollectionExtensions
{
    public static IServiceCollection AddXModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<XDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
```

This works because `internal` only blocks *other* assemblies — code inside
the same project can always see its own internals. The host calls
`AddXModule(connectionString)` and never needs to know the `DbContext` type
exists. Same pattern every module will repeat.

## EF Core: convention vs. explicit configuration

Most `OnModelCreating` configuration people write is unnecessary, because
EF Core already reads a lot straight from your C# types: a property named
`Id` is the primary key by convention; nullable reference type annotations
(`string` vs `string?`) decide column nullability; a `List<string>` becomes
a JSON column (EF Core 8+); a collection navigation property implies a
one-to-many relationship with an auto-created shadow foreign key. **Only
write explicit configuration for the two cases where it actually changes
something: overriding a convention default (e.g. a shadow FK is optional
by default — use `.HasForeignKey(...).IsRequired()` if it shouldn't be),
or telling EF Core something it has no way to infer** (custom column
names/types, max lengths). A `.Property(x => x.Foo)` call with nothing
chained after it is a no-op — worth checking whether an existing
`OnModelCreating` block is actually doing anything before assuming it is.

## EF Core CLI tooling gotchas

For the common "DbContext in a module/class-library project, ASP.NET Core
app as startup project" split (`--project <module>` /
`--startup-project <host>`):

- `Microsoft.EntityFrameworkCore.SqlServer` (or whichever provider) only
  needs to go on the **module** project — it reaches the host transitively
  through the project reference, since the host never calls `UseSqlServer`
  itself.
- `Microsoft.EntityFrameworkCore.Design` needs to go on **both** the
  module *and* the startup/host project. The tooling builds the startup
  project specifically to discover the `DbContext` through its service
  registrations, and needs design-time services available there too.
  Missing it on the host produces: *"Your startup project 'X' doesn't
  reference Microsoft.EntityFrameworkCore.Design."*
- Don't `migrations remove` and regenerate a migration that's already been
  applied to a real database — `remove` only deletes local files, it
  doesn't touch the database. The regenerated migration gets a new ID the
  database's `__EFMigrationsHistory` doesn't recognize, so `database
  update` tries to recreate tables that already exist and fails. Fine to
  fix with `database drop` + `database update` only on a throwaway local
  dev database with no real data; once real data exists, write a
  corrective migration instead.
- Every `dotnet ef` command that needs your `DbContext` will build your
  app's host and then deliberately throw
  `Microsoft.Extensions.Hosting.HostAbortedException` to stop before
  actually running it (confirmed via Microsoft's own docs: *"This
  exception should not be thrown or handled by user code"*). If
  `Program.cs` wraps `Main` in a broad `try/catch` (e.g. for a logging
  library's recommended startup pattern), that catch will intercept this
  benign exception too and can make every migration command look like a
  crash. Fix with an exception filter so it's never actually caught:
  `catch (Exception ex) when (ex is not HostAbortedException)`. Worth
  fixing for real, not just cosmetically — routine noise at a severity
  meant to mean "something is truly broken" trains people to ignore that
  severity, which is exactly wrong for the one time it's real.

## Cross-module communication

- A module exposes a small public interface in `Contracts/`
  (e.g. `IOrdersModuleApi`). Other modules or the host call that interface
  directly — in-process, no HTTP hop, since it's all one deployable.
- Add a project reference between modules only when a feature actually
  needs it — no speculative references "in case."
- Don't reach for a mediator/event bus (MediatR, etc.) up front. Start with
  direct interface calls; introduce eventing only once real coupling pain
  (e.g. needing to fan out to modules the caller shouldn't know about)
  justifies the added indirection.

## Presentation/host pattern

- Pick the simplest front door that satisfies the actual requirement (MVC,
  Blazor, a JSON API, a console app) — the choice is close to incidental
  architecturally, since the host only ever depends on module `Contracts`,
  never on module internals.
- A human-facing app (MVC/Blazor) calling a module's public interface
  directly and rendering a view teaches the same module-boundary lesson as
  an API controller serializing the same data to JSON — no need to stand up
  both unless something else actually needs to consume the data
  programmatically.
- Proof the architecture is sound: adding a second host later (e.g. a JSON
  API alongside an existing MVC app) should require only new thin
  controllers over existing `Contracts` interfaces — no changes inside any
  module.

## Domain modeling heuristics

These came up repeatedly while designing entities and are reusable well
beyond this project:

- **Enum vs. string for a field's values:** could a brand-new valid value
  show up someday that you don't control and haven't pre-approved? If yes,
  don't use an enum (a `string` usually). If the valid values are only ever
  ones your team deliberately decides on, an enum is right. A second,
  related test: does the code ever *behave* differently based on the
  value, or is it just a caption/label? A pure caption (a link's display
  label, say) stays a `string` even though the "who controls it" test
  alone might not rule out an enum.
- **Which module should own a piece of data:** does some module need this
  data to make an API call (integration config), or is it just something a
  human reads/clicks (descriptive metadata)? Integration config belongs
  with the module that actually makes the call, referencing the "owning"
  entity by a bare ID (e.g. `Guid ApplicationId`) rather than a project
  reference — add the real cross-module project reference later, only once
  a feature needs to actually call into the other module's `Contracts`.
  Descriptive/reference metadata belongs on the entity itself, alongside
  its other descriptive fields.
- **Derived facts vs. redundant stored fields:** if a fact is 100%
  computable from data you already store (e.g. a coarser "category" from a
  more specific "type"), don't also store it — compute it as a read-only
  property or extension member instead. A stored, independently-settable
  copy of a derived fact can only ever be redundant (harmless) or wrong (a
  bug); it never carries information the source data doesn't already have,
  and nothing stops it drifting out of sync the moment two people (or two
  code paths) set it inconsistently.
- **A 0-to-many relationship is a collection of its own type, not fixed
  named properties.** If you catch yourself naming properties
  `ThingOne`/`ThingTwo` for what's conceptually "some number of Things,"
  that's the tell — model `Thing` as its own type and hold a
  `List<Thing>` instead. Applies whether the "many" is small and fixed
  today (it usually won't stay that way) or already open-ended.

## Code organization convention: one type per file

Standard, still-current C# convention (not modular-monolith-specific, but
easy to let slide as files grow): one type per file, filename matching the
type name, even for small enums and static extension-method classes. It's
enforced by tools like StyleCop (`SA1402`) in a lot of real codebases.
Splitting a file that's accumulated several types (a class plus a couple of
enums plus a static helper class) back out to one-per-file is a purely
mechanical refactor — no behavior change, just move code between files in
the same namespace.

## Working habit: keep a Step-by-Step log

Keep a numbered, chronological log (this repo's `Step-by-Step/` folder) of
decisions made and why, session by session. It's what makes this playbook
possible to write honestly for the *next* project — without it, the reasons
behind decisions get lost to memory.

## Open upgrades (deliberately deferred, not forgotten)

Add these only once the corresponding pain actually shows up:

- Mediator/event bus for cross-module communication.
- Architecture tests (NetArchTest or similar) to enforce module boundaries
  and dependency direction automatically instead of relying on discipline.
- Separate databases per module, if schema-per-module stops being enough
  isolation.
- A separate JSON API host, if something other than the MVC app needs to
  consume this data programmatically.
