# 02 — Applications Domain Model

**Date:** 2026-09-09
**Status:** In progress. `Application` and `ApplicationProject` written and building
clean; a few additions (below) still to write.

## Entities so far

**`Application`** (`Domain/Application.cs`)
- `Id` (`Guid`, `required`/`init`)
- `Name` (`string`, `required`/`init`)
- `AliasNames` (`List<string>`) — other names the business/support calls this
  app by. Purely internal to Applications; not tied to any other system.
- `Description`, `BusinessOwner`, `ItOwner` (`string?`)
- `Status` (`ApplicationStatus` enum: `Active`, `Inactive`, `Deprecated`,
  `Retired`)
- `Projects` (`List<ApplicationProject>`)

**`ApplicationProject`** (`Domain/ApplicationProject.cs`)
- `Id` (`Guid`, `required`/`init`)
- `ProjectName` (`string`)
- `FrameworkVersion` (`FrameworkVersionName` enum — trimmed to the versions
  actually in use: `NetFullFramework47`, `NetFullFramework48`,
  `NetStandard`, `Net8`, `Net10`)
- `ProjectType` (`ProjectTypeName` enum — trimmed/expanded to match real
  project shapes: `WebFormsApplication`, `WebMvcApplication`,
  `WebRazorPagesApplication`, `ConsoleApplication`, `ClassLibrary`,
  `WindowsFormsApplication`, `WpfApplication`, `BlazorApplication`,
  `MauiApplication`, `WebApiMvc`, `WebApiMinimal`, `WorkerService`,
  `WindowsService`)

## Design heuristic established: enum vs. string

Came up twice (`FrameworkVersion`/`ProjectType`, then `ApplicationLink`
labels) and is worth reusing on future fields:

> Could a brand-new valid value show up someday that you don't control and
> haven't pre-approved? If yes, don't use an enum. If the valid values are
> only ever ones your team deliberately decides on, an enum is right.

Concretely: `ProjectType` and `ApplicationStatus` are enums because the
categories are catalog-defined and closed. `FrameworkVersion` is *also*
kept as an enum, but that's a deliberate, informed exception — the values
listed are genuinely all that's in use today, with the accepted tradeoff
that a new .NET version means an enum edit rather than a data-only change.
A second heuristic for **enum vs. plain string in general**: does the code
ever *behave* differently based on the value, or is it just a caption? A
link's `Label` ("Wiki", "LeanIX", "Logz") is just a caption — nothing
branches on it — so it stays a `string`.

## Design heuristic established: which module owns a piece of data

Came up deciding where ADO area paths and app links belong. The question
that settled it both times: **does some module need this data to make an
API call, or is a human just going to click/read it?**

- **ADO area paths** (per-app mapping of ADO project + team → area path,
  used to query bugs/work items for that app) → integration config that
  only the module *making that call* needs. Belongs in **Health**, not
  Applications, even though it's "about" an application — modeled as its
  own entity (`ApplicationAdoAreaPath`) referencing `Application` by a bare
  `Guid ApplicationId`, **not** a project reference to the Applications
  assembly. A project reference gets added only once Health needs to
  actually call into Applications' public Contracts (e.g. to look up an
  app's name) — not for storing a foreign-key-shaped `Guid`.
  ```csharp
  public class ApplicationAdoAreaPath
  {
      public required Guid Id { get; init; }
      public required Guid ApplicationId { get; init; }
      public required string AdoProjectName { get; set; }
      public required string TeamName { get; set; }
      public required string AreaPath { get; set; }
  }
  ```
  Modeled as a collection (`List<ApplicationAdoAreaPath>`), not fixed named
  properties like `SupportBoardAreaPath`/`ProjectBoardAreaPath` — area
  paths can come from more than one ADO project/team per app, an
  open-ended (0-to-many) relationship, same shape as `AliasNames`/`Projects`.

- **App reference links** (LeanIX fact sheet, wiki page, Logz dashboard —
  meant to display on the app's page) → nobody's code calls these, so
  they're descriptive metadata about the app itself, same category as
  `Name`/`Description`. Belongs on **Applications**:
  ```csharp
  public class ApplicationLink
  {
      public required Guid Id { get; init; }
      public required string Label { get; set; }
      public required string Url { get; set; }
  }
  ```
  Also modeled as a collection (`List<ApplicationLink>` on `Application`)
  for the same open-ended-set reason.

## Health module mechanism (design note for later — Health not started yet)

Clarified while placing ADO area paths, worth recording now so it isn't
lost before we actually build Health:

- **An app's "health" is derived from ADO bugs/regressions filed under that
  app's area path(s)** — not a separately-tracked status value. Likely
  means `HealthStatus` doesn't need to be its own stored entity; it can be
  computed from the `WorkItems` pulled from ADO for the app's area paths.
- **Logz (error/log monitoring) is not queried directly by Health**, at
  least for now. An error notification in Logz triggers creation of an ADO
  bug (manually today; automating that hand-off is a stated wish, not yet
  designed). So Logz shows up only as an `ApplicationLink` (a link to the
  app's Logz dashboard for a human to check), not as a data source Health
  calls.
- Reinforces the ADO-area-path placement decision above: Health needs the
  area path(s) specifically to query ADO for bugs/regressions scoped to
  that app.

## Still open

- `HasTests` (or similar) on `ApplicationProject` — not yet decided.
- `ApplicationLink` and `ApplicationAdoAreaPath` are designed but not yet
  written into the codebase.
