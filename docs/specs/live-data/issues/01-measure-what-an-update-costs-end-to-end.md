# 01: Measure what an update costs, end to end

Status: done — baseline measurements recorded; follow-up decisions are ADR-0150 to ADR-0153

**What to do:** find out, step by step, where the time of one live update goes, in ExGrid and in
ExPivot, so that tickets 02 and 03 are decided on numbers (CLAUDE.md, "Measure before claiming
anything about performance"). Nothing is changed in `src/` by this ticket; every number is
observational and never gates.

**Blocked by:** None

## What is known

- **The Row Key's own cost** (ADR-0140, D10; Release, one Mac, the least of repeated runs). At
  401,001 report rows the grid's check of a pushed Window takes 8.3 ms by `PivotRowKey` and 9.5 ms
  by instances, and making the keys adds 13.3 ms to building the report (337 ms). At 101,001 rows:
  1.0 ms to 105 ms.
- **`GridSource.From`'s requery** (M4): at 10⁶ rows, 0.23 ms for one change and 3.2 ms for 1,000,
  incrementally, against 365 ms for a whole requery. The grid's pass over a new Window was 18.1 ms at
  451,115 rows; a keyed bundled source now vouches, and the grid skips it (LV-10).
- **A tick in the browser and on a circuit** (M1, M2): keyed rows send 3,134 bytes a tick on the wire
  where instance-keyed rows sent 40,712 (40 rows, 20 columns, 3 cells changed).
- **ExPivot's redraw** (M3): at `/pivot-live`'s rate, a median of 5 of 11 painted rows unchanged per
  redraw with subtotals.
- **Not known before this measurement:** how a redraw's time divides between its steps, and the browser numbers for either
  product at the sizes that hurt.

## What to measure

- **ExGrid, `GridSource.From` keyed** (`/grid-live-local`; 10⁵ and 10⁶ rows; 1, 100 and 1,000
  changes a batch; `?interval=0` so that each batch is shown at once):
  - the source's `Apply` and its publication (`InMemoryGridSource.PublishPending`, `LiveRequery`);
  - the grid's taking in of the new Window (`ApplyState`, `TakeInWindow` in `ExGrid.RowKey.cs`) and
    anything else it does per new Window;
  - the .NET render of the rows that changed, and the frame in the browser
    (`measure-live.spec.mjs`, `EXGRID_MEASURE=live`, on a published WebAssembly build);
  - the bytes per update on the Server host, by M2's method (`spikes/render-bench`, its Server spike).
- **ExPivot, a live redraw**, at reports of about 10⁴, 10⁵ and 4×10⁵ rows (two row fields, as in
  D10's measurement), step by step as `src/ExPivot/Components/ExPivot.Asking.cs` runs them:
  - the bundled source folding the batch (`SnapshotPivotSource.Apply`) and answering;
  - `PivotEngine.CubeAsync` (`ExPivot.Asking.cs:392`; the axis trees in `PivotCube.TreeAsync`);
  - `PivotEngine.ReportAsync` (`:544`; `ReportBuilder`), with the keys it makes;
  - `HasSameRowsAsAsync` (`:554`) and `LabelWidthsAsync` (`:557`);
  - `Show` (`:636`): the Change Highlight's history and the columns;
  - the grid's check of the report's keys (`RequireDistinctKeys`, `ExGrid.RowKey.cs:145`) and the
    render.

## How, and the traps already hit

- **CoreCLR in Release with `DOTNET_TieredCompilation=0`, the least of 7 to 15 runs.** With tiering
  on, the first configuration measured slower than the later ones (2026-10-06).
- **Measure a step in isolation, not by toggling code and rebuilding.** Building a 401,001-row report
  varied by ±50 ms from run to run, so a before-and-after of two builds hid a 13 ms step.
- **Write results to a fixed path.** Under `nix develop`, `Path.GetTempPath()` is a directory that
  is removed when the shell exits: a measurement written there is lost without a word.
- **The shape of D10's harness**, to recreate rather than to commit: a throwaway xUnit test in
  `tests/ExPivot.Engine.Tests` (it sees the engine's internals) that builds a report from records of
  two text fields (`A00000`… × `B0000`…) with `PivotEngine.Compute`, then times a pass over
  `report.Rows` that puts each `row.Key` into a `Dictionary<object, int>`, beside a pass that puts
  each row into a `HashSet<object>` with `ReferenceEqualityComparer` — the grid's two checks.
- **The browser**: `measure-live.spec.mjs` and `measure-pivot.spec.mjs` run only when asked
  (`EXGRID_MEASURE=…`), against a published build, and record into `metrics.json`. A local layer-3
  run on the Mac is headless (`EXGRID_HEADLESS=1`); a scrollbar check means nothing there, but these
  do not check scrollbars.

## Output

- [x] A `verification/<date>-<platform>-live-update-costs/README.md` in the shape of
  `2026-10-05-macos-live-update-measure`: the environment, a table per product, raw files beside it
- [x] For each product, the step that dominates an update, at each size
- [x] LV-15's parts recorded: apply to frame on `/grid-live-local`, the bytes per update on the
  Server host, the requery and the grid's pass per update
- [x] What the numbers say about tickets 02 and 03, as a proposal — not a decision

## Comments

2026-10-06: [Measurements and reproduction artifacts](../../../../verification/2026-10-06-macos-live-update-costs/README.md)
record the isolated CoreCLR steps, real component renders, published WebAssembly frames and Server
bytes at `bd4b2e45`, after PR #64. No file under `src/` changed. The raw data, failed attempts and
disposable harness are retained beside the README; tables were recalculated from the raw samples.

Cube and Report construction dominate ExPivot's redraw at all three sizes. The largest published
WebAssembly report failed with an out-of-memory exception on update seven in all three independent
runs. Its completed frames are conditional observations, not a passed run or a steady-state claim.
The failure's cause remains open and its reproduction must be part of the follow-up. The real
ExPivot component's CoreCLR render and row counts were measured separately; its render includes
the child grid's Window/key check. No separate WASM .NET-render time was inferred.
