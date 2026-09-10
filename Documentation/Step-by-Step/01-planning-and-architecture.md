# 01 — Planning and Architecture

**Date:** 2026-09-09
**Status:** Planning complete, solution not yet scaffolded (see task below).

## What this project is

**ApplicationCatalog** — a real work problem, used as the vehicle for
learning modular monolith architecture in .NET. Solution root lives at
`C:\git\Momo1` (the parent folder name isn't meaningful — the solution and
all projects are named `ApplicationCatalog.*`).

The catalog tracks:

- Applications: metadata, business owner, IT owner, what the app does.
- Data dependencies down to the table/column level: which apps write to
  which tables, which columns an app *actually* uses meaningfully (vs. just
  touches), stored procs/views involved, and a PII flag per column.
- System health per application: regressions, bugs, work, deployments —
  sourced from the Azure DevOps API (mocked for now, real integration
  later).

## Modules chosen and why

Three modules fell out of the domain naturally, each owning distinct data:

| Module | Owns | Depends on |
|---|---|---|
| **Applications** | Application entity: name, description, business/IT owner, status | nothing |
| **DataLineage** | ExternalSystem, Table, Column (+ PII flag), StoredProc/View, App-writes-Table and App-uses-Column relationships | Applications |
| **Health** | HealthStatus, WorkItems (bugs/regressions), Deployments (from Azure DevOps, mocked) | Applications |

Build order: **Applications first, end-to-end** (simplest module, forces one
full pass through every layer while stakes are low) → **DataLineage**
(the deepest domain model) → **Health** (introduces an external-integration
seam last, once the basics are solid).

## Architecture decisions

- **Module = one class library project**, not split into separate
  Domain/Application/Infrastructure assemblies. Internal organization is by
  folder:
  - `Domain/` — entities, value objects. No outward dependencies.
  - `Infrastructure/` — the module's own `DbContext`, EF configurations,
    repository implementations.
  - `Contracts/` — the module's public surface (an interface like
    `IApplicationsModuleApi` + DTOs). **This is the only folder whose types
    are `public`.** Everything else is `internal`.
  - The module boundary is enforced by C# visibility + project references:
    a referencing project can only see `public` types in the referenced
    assembly, so `internal` types in `Domain`/`Infrastructure` are
    physically unreachable from outside the module.

- **Presentation: ASP.NET Core MVC only** (`ApplicationCatalog.Web`). No
  separate JSON API project. MVC controllers call each module's `Contracts`
  interface directly (in-process — no HTTP hop, since it's all one
  process) and render Razor views.
  - Rationale: the module-boundary lessons (encapsulation, independent
    persistence, explicit inter-module dependencies) live *inside* the
    process and don't require an HTTP boundary to practice. An MVC
    controller calling a module's public interface teaches the same lesson
    as an API controller would, without serialization noise. If a JSON API
    is ever needed, it can be added later as thin controllers over the same
    module contracts — proof that the architecture doesn't need to change
    to support it.

- **Persistence: one SQL Server (LocalDB) database, separate schema per
  module** (`applications.*`, `datalineage.*`, `health.*`). Cheaper to run
  than separate databases per module, but still forbids cross-module SQL
  joins — each module owns its schema.

- **Cross-module communication: direct public interface per module**
  (e.g. `IApplicationsModuleApi.GetApplicationSummary(id)`). No
  mediator/event bus (e.g. MediatR) yet — that's a deliberate later upgrade,
  added only once real coupling pain justifies it, not speculatively.

- **No cross-module project references until actually needed.** DataLineage
  and Health will reference Applications once we build the first feature
  that actually calls into it — not before.

## Solution layout

```
ApplicationCatalog.sln
src/
  Modules/
    Applications/ApplicationCatalog.Modules.Applications/
    DataLineage/ApplicationCatalog.Modules.DataLineage/
    Health/ApplicationCatalog.Modules.Health/
  Host/
    ApplicationCatalog.Web/
tests/
  ApplicationCatalog.Modules.Applications.Tests/
```

## Mentoring approach for this project

User writes all application code; the assistant explains concepts, proposes
architecture options, and reviews what's written before moving to the next
step. Task-by-task handoff, not code generation.

## Scaffold commands (run from `C:\git\Momo1`)

```powershell
dotnet new sln -n ApplicationCatalog

dotnet new classlib -n ApplicationCatalog.Modules.Applications -o src/Modules/Applications/ApplicationCatalog.Modules.Applications
dotnet new classlib -n ApplicationCatalog.Modules.DataLineage -o src/Modules/DataLineage/ApplicationCatalog.Modules.DataLineage
dotnet new classlib -n ApplicationCatalog.Modules.Health -o src/Modules/Health/ApplicationCatalog.Modules.Health

dotnet new mvc -n ApplicationCatalog.Web -o src/Host/ApplicationCatalog.Web

dotnet new xunit -n ApplicationCatalog.Modules.Applications.Tests -o tests/ApplicationCatalog.Modules.Applications.Tests

dotnet sln add src/Modules/Applications/ApplicationCatalog.Modules.Applications
dotnet sln add src/Modules/DataLineage/ApplicationCatalog.Modules.DataLineage
dotnet sln add src/Modules/Health/ApplicationCatalog.Modules.Health
dotnet sln add src/Host/ApplicationCatalog.Web
dotnet sln add tests/ApplicationCatalog.Modules.Applications.Tests

dotnet add src/Host/ApplicationCatalog.Web reference src/Modules/Applications/ApplicationCatalog.Modules.Applications
dotnet add src/Host/ApplicationCatalog.Web reference src/Modules/DataLineage/ApplicationCatalog.Modules.DataLineage
dotnet add src/Host/ApplicationCatalog.Web reference src/Modules/Health/ApplicationCatalog.Modules.Health

dotnet add tests/ApplicationCatalog.Modules.Applications.Tests reference src/Modules/Applications/ApplicationCatalog.Modules.Applications
```

## Next step

Scaffold the solution with the commands above, confirm `dotnet build` is
clean, then design the `Application` domain entity together.
