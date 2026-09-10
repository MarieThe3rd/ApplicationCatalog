# ApplicationCatalog

An internal application catalog: tracks applications, business/IT ownership,
data lineage down to the table/column level (including PII flagging), and
per-application system health (bugs, regressions, deployments — sourced from
Azure DevOps, mocked for now).

Built as a hands-on learning project for **modular monolith architecture in
ASP.NET Core** — a real work problem used as the vehicle, not a toy domain.

## Architecture

Modular monolith: one ASP.NET Core MVC host, three independent modules, each
owning its own data and exposing a small public interface (`Contracts/`) as
the only thing the host or other modules are allowed to depend on.

| Module | Owns | Depends on |
|---|---|---|
| **Applications** | Application metadata, owners, status, projects, links | — |
| **DataLineage** | External systems, tables, columns (+ PII flag), stored procs/views, which apps write/read what | Applications |
| **Health** | Health status, work items, deployments (from Azure DevOps) | Applications |

Full architecture rationale and reusable patterns:
[`Documentation/ModularMonolithPlaybook.md`](Documentation/ModularMonolithPlaybook.md).
Session-by-session build log, including decisions and why:
[`Documentation/Step-by-Step/`](Documentation/Step-by-Step/).

## Tech stack

- ASP.NET Core MVC (.NET 10)
- EF Core, SQL Server (LocalDB for local dev) — one database, separate schema per module
- Serilog (structured logging, two-stage startup initialization)

## Project layout

```
ApplicationCatalog.slnx
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

## Status

- **Applications** — domain model, EF Core persistence, and migrations
  complete and applied to LocalDB. `Contracts` (public interface + DTO) in
  progress. MVC controllers/views not yet started.
- **DataLineage**, **Health** — scaffolded only, no implementation yet.

## Getting started

Prerequisites: .NET 10 SDK, SQL Server LocalDB, the `dotnet-ef` tool
(`dotnet tool install --global dotnet-ef` if you don't have it).

```powershell
dotnet build

dotnet ef database update `
  --project src/Modules/Applications/ApplicationCatalog.Modules.Applications `
  --startup-project src/Host/ApplicationCatalog.Web

dotnet run --project src/Host/ApplicationCatalog.Web
```
