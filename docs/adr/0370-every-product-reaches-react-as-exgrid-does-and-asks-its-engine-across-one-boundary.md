# Every product reaches React as ExGrid does, and asks its engine across one boundary

*The scope was decided by the user, 2026-10-25: React support covers the whole family, ExSheet,
ExPivot, ExChart and ExBoard as well as ExGrid, and the products may be built in parallel. The shape
below was put to the user the same day as four options with a recommendation; until the user answers,
this ADR is proposed, and work proceeds on the recommendation where it can be undone.*

## Context

[ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md) moved
ExGrid's core to TypeScript, the Script Core, and made React and Blazor both adapters over it. It kept
ExSheet, ExPivot, ExChart, ExBoard and the MudBlazor Wrappers as Blazor products, to be verified again
on Blazor's adapter, and left "whether any of them reaches React" to a later decision (its section 1).
This is that decision.

Measured on `main` at eaea3b2, each product splits into a UI that depends on Blazor and an engine that
does not:

| Product | UI (Razor package: C# and Razor) | Script beside it | Engine (no Razor) |
|---|---|---|---|
| ExSheet | ~12,500 (`ExSheet`) | none of its own | ~20,900 `ExSheet.Engine`, ~3,300 `ExSheet.Xlsx` |
| ExPivot | ~7,000 (`ExPivot`) | none of its own | ~15,500 `ExPivot.Engine` |
| ExChart | ~3,500 (`ExChart`) | 859 (`ex-chart.js`: pointer, 3D surface, PNG) | ~5,900 `ExChart.Engine` |
| ExBoard | ~3,500 (`ExBoard`) | none | none of its own; it draws the others |
| Data | | | ~9,900 `ExGrid.Data`, ~2,500 `.Arrow`, ~4,300 `.Xlsx` |
| Wrappers | ~5,000 (`ExSheet.MudBlazor`, `ExPivot.MudBlazor`, `ExChart.MudBlazor`), which stay Razor | none | |

Each product UI is already ExGrid's Consumer through ExGrid's public surface
([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md),
[ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
[ADR-0063](./0063-what-expivot-asks-of-exgrids-core.md),
[ADR-0250](./0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md)).
ExSheet's and ExPivot's engines answer as Excel or not at all
([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md),
[ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)); ExChart's
lays out a scene of shapes and has no Excel to answer to.
[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md) already says where
the pivot engine runs: on the server for server data, and where the data is held for local data, one
engine in both places.

The render-bench baseline of 2026-10-25 (`spikes/render-bench/results/20261025-core/`) measured the
engines where Blazor runs them today. An ExSheet edit that recalculates 20,000 cells shows in 130 ms on
Blazor Server and 3.3 s on Blazor WebAssembly, on the interpreter; where the WebAssembly time goes was
not run down. An ExPivot Change Batch of one trade redraws in 4 ms and 79 ms.

## Options considered

| | A. UIs in TypeScript for both frameworks, engines stay C# (recommended) | B. UIs and every engine in TypeScript | C. A React UI beside each Razor UI | D. Engines on a server only |
|---|---|---|---|---|
| UI | Each product's UI moves as ExGrid's did; React and Blazor adapt it | As A | A TypeScript UI for React; Blazor keeps its Razor UI | As A |
| Engines under React | .NET WebAssembly in a Worker, or a server | Ported | As A | A server the application runs |
| Cost | ~26,500 lines of UI and ~5,900 of chart layout; a TypeScript Arrow reader; the engines' WebAssembly build | A's, and ~56,000 more lines of engine and data, the formula engine re-proved against Excel | A React UI per product, the Razor ones kept | A's, less the WebAssembly build |
| What it leaves | One implementation per product; a .NET runtime downloaded where an engine runs in the browser | One implementation, no .NET under React | Two UIs per product from then on, kept alike by hand | No ExSheet or local ExPivot without a .NET server |

B was not recommended because it re-proves the two engines where the risk is highest, before a
measurement says the WebAssembly route cannot hold (ADR-0350 section 8). C was not recommended because
two UIs per product is the drift ADR-0350 removed for ExGrid, multiplied by four. D was not recommended
because it makes a .NET backend the price of a spreadsheet in a React application, and ADR-0151 already
keeps local data local.

## Decision

### 1. Each product's UI moves to TypeScript, and React and Blazor both take it

Each product gets a Script Core of its own: ExSheet's, ExPivot's, ExChart's and ExBoard's. It holds what
the Razor package holds today, the component and the logic beside it (ExSheet's Pointing, Format Cells,
Sheet Tabs and Find; ExPivot's Field List, Details and toolbar; ExChart's legend, tooltip, Chart
Filters and its bridge from a grid selection; ExBoard's layout, links, outputs and Board Diff), and
decides what each input means. It depends on no UI framework.

A product's Script Core is ExGrid's Consumer, as the Razor product is: it draws through ExGrid's Script
Core's public surface and nothing private, so what it asks of ExGrid is stated as ADR-0063 states
ExPivot's, and is a member of `@ex-grid/core`'s surface. ADR-0350 sections 2 to 6 hold for it as
written: it owns the DOM inside its own root, writes the product's DOM contract and reads its
stylesheet unchanged, and its layer-3 specs run against a React host as against both Blazor hosts.

Each product has a React adapter and a Blazor adapter, both Framework Adapters as ADR-0350 section 3
defines them. Blazor's ExSheet, ExPivot, ExChart and ExBoard become their product's Blazor adapter. This
replaces ADR-0350 section 14's "rewritten against the new surface" for them: the rewrite is the move.

The scripts beside the products today (`ex-chart.js`'s pointer, 3D surface and PNG entries,
[ADR-0255](./0255-two-javascript-entries-the-charts-pointer-and-the-3d-surface.md),
[ADR-0282](./0282-a-thirteenth-javascript-entry-paints-a-chart-to-png-and-saves-its-files.md)) become
part of their product's Script Core, under ADR-0350 section 6's rules rather than ADR-0021's allowlist.

The MudBlazor Wrappers stay Razor. Each becomes a Wrapper over its product's Blazor adapter, as
`ExGrid.MudBlazor` does over ExGrid's (ADR-0350 section 9), and fills the Chrome seams with MudBlazor's
components. Each MUI Wrapper is new work beside it (section 4).

### 2. ExSheet's and ExPivot's engines stay C#, and a Script Core asks one across an Engine Host

ExSheet.Engine with ExSheet.Xlsx, ExPivot.Engine, and ExGrid.Data with its Arrow and Xlsx packages are
not ported (ADR-0350 section 8 stands); what TypeScript gains of ExGrid.Data is a reader and writer of
the Snapshot's Arrow form only, section 3. A product's Script Core never calls an engine directly. It asks
an **Engine Host**: where the engine runs for a UI that is not in .NET, reached by asynchronous
messages whose answers are data (a Window of Values, a report Window, a Snapshot as Arrow,
[ADR-0065](./0065-a-snapshot-travels-as-apache-arrow.md)).

- **One protocol, three hosts.** Under Blazor the host is the adapter's own .NET side, so the engine
  runs where it runs today: on the server under Blazor Server, in the browser's .NET under
  WebAssembly. Under React with local data the host is a Web Worker running the engines as .NET
  WebAssembly: one Worker for the page, holding ExSheet's and ExPivot's engines and ExGrid.Data with
  its Xlsx reader in one runtime, so a page that shows both, an ExBoard for one, boots .NET once and
  keeps its Snapshots in one heap. For server data, under either framework, the host is a server, as
  ADR-0151 already has it for the pivot. The engine and the protocol are the same in all three, so a Value or a report is
  computed by one implementation wherever it is shown.
- **Under React, a Worker, never the page's thread.** A recalculation or a report runs off the
  thread that answers keys, so the grid keeps its frame while the engine works. Under Blazor
  WebAssembly the engine stays on the page's .NET, where it runs today; whether it moves to a Worker
  there too is decided on section 6's measurement.
- **An answer carries what it was asked against.** Each request names the version of the document or
  the Snapshot it was taken on, and an answer that arrives for an older one is refused, not drawn
  (spine rule 6). Nothing waits on a clock.
- **Writes are never refused for being late, and reads coalesce.** An edit, a Change Batch or a field
  moved is a write: the host takes every one in the order it was made. A recalculation or a report in
  flight is not restarted; when it ends, the next one runs for the newest version, so a stream of edits
  is answered at the engine's pace rather than not at all. Until the shown version is answered, the
  product says it is calculating, as Excel's status bar does, and never shows an older answer as the
  current one. The mark is the product's ticket's to design.
- **The Consumer owns the server's transport and authentication**, as ADR-0151 says: the family ships
  the protocol, a client for it and a .NET handler that answers it, not a server.

### 3. ExChart's layout moves to TypeScript, and each format the family reads has a reader in both languages

ExChart.Engine's layout, the scales, ticks, thinning, Series Kind steps and gaps, the scene of shapes
and the 3D mesh, moves into ExChart's Script Core. It is redrawn on every resize, hover and Chart
Filter, it has no Excel to be re-proved against, it depends on nothing, and a chart should not need a
.NET runtime in the browser. Its tests become shared cases first, green on the C# engine, as ADR-0350
section 5 does for ExGrid.

The Chart Definition and the reading of a Chart Spec
([ADR-0320](./0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md)) stay in C# as well: a
host that answers an AI's tool may be a .NET server. Each spec the family reads, Chart Spec and Board
Spec ([ADR-0340](./0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md)), is
therefore read in both languages, and both readers run one set of shared cases, refusals and their
paths included. So is the Snapshot's Arrow form: `@ex-grid/data` reads and writes it so that a Snapshot
made in the browser reaches an Engine Host and one it answers can be shown, and it runs the same cases
as `ExGrid.Data.Arrow`. These are readers of a format, not ports of an engine.

### 4. The packages

npm, under ADR-0350 section 9's scope, with each package's direction enforced by its dependencies as
NuGet's are by project references:

| npm | Holds | NuGet counterpart |
|---|---|---|
| `@ex-grid/data` | The Snapshot as TypeScript reads and writes it in Arrow | `ExGrid.Data.Arrow` |
| `@ex-grid/sheet`, `@ex-grid/sheet-react`, `@ex-grid/sheet-mui` | ExSheet's Script Core, its React adapter, its MUI Wrapper | `ExSheet`, `ExSheet.MudBlazor` |
| `@ex-grid/pivot`, `@ex-grid/pivot-react`, `@ex-grid/pivot-mui` | ExPivot's | `ExPivot`, `ExPivot.MudBlazor` |
| `@ex-grid/engine` | The Engine Host Worker: ExSheet.Engine, ExSheet.Xlsx, ExPivot.Engine, ExGrid.Data and ExGrid.Data.Xlsx as one .NET WebAssembly build, and its client | `ExSheet.Engine`, `ExPivot.Engine`, `ExGrid.Data` |
| `@ex-grid/chart`, `@ex-grid/chart-react`, `@ex-grid/chart-mui` | ExChart's, its layout included | `ExChart`, `ExChart.Engine`, `ExChart.MudBlazor` |
| `@ex-grid/board`, `@ex-grid/board-react` | ExBoard's | `ExBoard` |

Source lives at `src/npm/<name>` (ADR-0350 section 13). A Blazor adapter carries its product's built
script as a shipped asset of its own (ADR-0350 section 9, ADR-0123), so a Blazor Consumer still installs
nothing from npm. Each MUI Wrapper is held to ADR-0030 as `@ex-grid/mui` is.

### 5. The order inside 0.3.0

ADR-0350 section 7's three steps hold for ExGrid as written, and each product takes the same three:

1. **React.** The product's Script Core and React adapter pass its layer-3 specs on the React host, in
   both browsers. A product starts once ExGrid's Script Core has what it asks of ExGrid; its own logic
   may move earlier. Beside ExGrid's step 1, and waiting on nothing: the Engine Host Worker is built and
   measured (section 6), ExChart's layout moves to TypeScript on shared cases, and `@ex-grid/data`
   reads and writes Arrow.
2. **Blazor.** The product's Blazor adapter replaces its Razor component once its step 1 holds and
   ExGrid's Blazor adapter stands. Its specs pass on both Blazor hosts, and so do those of every product
   it draws or is drawn by. ExGrid's step 2 waits only on ExGrid's step 1, as ADR-0350 has it.
3. **Removal.** The C# UI code goes: ExGrid's core as ADR-0350 has it, each product's Razor logic, and
   the C# layout of ExChart.Engine. ADR-0021 then governs no script and is superseded; what it protected
   is held by ADR-0350 section 6 for every Script Core.

0.3.0 is not tagged until every product's three steps hold (ADR-0350 section 7). Prereleases publish from tags meanwhile
([ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)), so a
product reaches React developers as a prerelease when its step 2 holds.

### 6. An engine in the browser is measured before its product is called done

The 3.3 s ExSheet edit on Blazor WebAssembly is the case to beat, and its cause is not known. Before a
product's step 1 is called done, render-bench records its engine in the Engine Host Worker, on the
interpreter and compiled ahead of time, beside the C# baseline: the `/core/sheet` and `/core/pivot`
scenarios, the time to the screen, the bytes downloaded with every engine the Worker holds, and the
time to the first answer. The same record says where Blazor WebAssembly's 3.3 s goes, which decides
whether its engine moves to a Worker too (section 2). A case the
user cannot work with is put to the user with a port's cost; a port is decided on that record, not
before (ADR-0350 sections 8 and 15).

### 7. The Docs Site shows every product under both frameworks

ADR-0350 section 12's framework switch reaches every product's pages: each Example exists in both
frameworks, and a page whose feature is not in the React packages yet says so where its Example would
be, as every page outside ExGrid says today.

## Consequences

- Every product has one UI implementation from 0.3.0, as ExGrid has; until step 4, the Razor products
  still ship on `main`, and the shared cases and the shared layer 3 keep the two from drifting.
- 0.3.0 grows by ~26,500 lines of UI and ~5,900 of chart layout to move, a TypeScript Arrow reader,
  an Engine Host built for WebAssembly, and three MUI Wrappers written new. Its products can
  be built in parallel once ExGrid's Script Core states what each asks of it; the measurement, ExChart's
  layout and `@ex-grid/data` can start at once.
- A React application that runs ExSheet or a local ExPivot in the browser downloads a .NET runtime in
  a Worker, once for the page. ExGrid and ExChart under React download none.
- The family publishes thirteen npm packages beside ADR-0350's three, and the release workflow publishes
  them from the same tag.
- The two AI specs have a reader in each language, held together by shared cases.
