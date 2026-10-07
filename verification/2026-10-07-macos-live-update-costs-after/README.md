# Live updates after the changes: what one update costs, before and after (ticket 13)

Date: 2026-10-07. Branch `claude/live-data-next-cc-measure`, from `claude/live-data-next-cc` at `526f3b75`
(tickets 04 to 12 built). No file under `src/` was changed; every step was reached from outside, by
reflection where it is private. The harness is recreated, not committed: a copy is in
[`harness/`](harness/README.md).

This is ticket 13 of [the live-data spec](../../docs/specs/live-data/issues/13-measure-after-the-changes.md):
[ticket 01's measurement](../2026-10-06-macos-live-update-costs-cc/README.md) repeated after
[ADR-0161](../../docs/adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md) (the next report
made from the last), [ADR-0160](../../docs/adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)
(no row beyond the Window), ADR-0141's note of 2026-10-07 (`VouchesDistinctRows`), the rewrite of ADR-0142
and ADR-0130's note of 2026-10-07 (the Selection Summary walks only what it needs). It decides nothing: no
ADR, no `CONTEXT.md` entry and no Definition of Done criterion is changed.

**Before** is the code at `41c8d8c8`, as three records of 2026-10-06 measured it:
[ticket 01's](../2026-10-06-macos-live-update-costs-cc/README.md), [the out-of-memory
record](../2026-10-06-macos-pivot-oom/README.md) and [the paint-text record](../2026-10-06-macos-paint-text-cost/README.md)
(its V0 is `41c8d8c8`). Where a before figure was missing, or a day's drift could hide a difference, the
before code was run again today on the same machine and harness; those columns say "the same day".
**After** is `526f3b75`.

Performance never gates (AGENTS.md). Every number here is observational.

## The results in brief

Milliseconds. CoreCLR is the least of 15 runs (Release, `DOTNET_TieredCompilation=0`); the browser is
WebAssembly (published, no AOT, headless Chrome), Apply to the painted frame that shows the change, least /
median of 15.

| Product | Size | Changes | Whole update, CoreCLR: before → after | Browser, Apply → painted: before → after |
|---|---|---|---|---|
| ExPivot, live redraw (PV-48) | 10,001 report rows | 1 | 3.53 → **0.481** | 57 / 78 → **28 / 29** |
| | | 1,000 | 4.32 → **1.93** | 68 / 71 → **39 / 46** |
| | 101,001 | 1 | 60.2 → **4.81** | 529 / 563 → **143 / 152** |
| | | 1,000 | 62.9 → **9.03** | 555 / 567 → **175 / 183** |
| | 401,001 | 1 | 268.5 → **19.0** | 2,338 (one redraw; longer runs ran out of memory) → **532 / 537** |
| | | 1,000 | 276.0 → **28.8** | out of memory → **600 / 610** |
| ExGrid, keyed `GridSource.From` (LV-15) | 10⁶ rows | 1,000 | 3.34 → 3.31 | 22.7 / 28.6 → 22.4 / 28.5 |
| | | 1 | 0.745 (median 3.07, most 10.1) → 0.562 (median 0.807, most 0.933) | 5.0 / 91.4 (most 136) → **3.4 / 6.8** (most 73) |
| A pushed 10⁶-row Window (ticket 07) | 10⁶ rows | 1,000 | take-in 42.9 unvouched → **0.012** vouched | unvouched 590 / 632; **vouched 23.7 / 29.7** |

- **The out-of-memory is gone (LV-23).** In the browser, the managed heap after a full collection stays at
  89.5 MB for 20 redraws of 101,001 report rows (before: +41 MB a redraw, without end), and at 329.7 MB for
  15 redraws of 401,001 (before: out of memory on the 7th). After every redraw one report is alive, the one
  on screen. An 80-redraw run at 401,001 rows did not run out either. On CoreCLR no earlier report or cube
  is alive after any redraw.
- **The collector's cost per redraw (steady state, 101,001 rows, CoreCLR, no forced collection): 28.6 ms →
  0.57 ms** of pause per redraw on average (redraws 11–150), and the heap stays at 0.2–0.3 GB where it grew
  to 8.1 GB. With a full collection before each redraw, the redraw no longer collects at all (before: 5
  collections and a median pause of 17.8 ms at 101,001 rows, 18 and 68.8 ms at 401,001).
- **What dominates now**, on CoreCLR: the source's answer and the next cube, about equally, at one change
  (at 401,001 rows 9.3 and 9.6 of 19.0 ms); the next cube at 1,000 changes (19.4 of 28.8). Laying the report
  out, which was 31–59% of a redraw, is now 8–30%.
- **In the browser the redraw did not shrink as far as on CoreCLR**: at 101,001 and 401,001 rows and one
  change, 3.7–4.4 times faster against 12.5–14 times. The steps the page can time (`NextCube` 88 ms and `NextReport` 23 ms at 401,001 rows) are about a
  fifth of Apply → painted (532 ms); the rest was not separated (see "Not measured").
- **Slower** (in full under "What got slower"): a data redraw laid out afresh — after a compaction of the
  source, about once every 64 batches here — now costs more than every redraw did before (CoreCLR 394.5
  against 268.5 ms at 401,001 rows; browser 3,329 against 2,338 ms), because it compares every row for the
  Change Highlight; a pushed Window's check by key, when it is not vouched, takes 8–21% longer on CoreCLR;
  `Show` takes 0.3–0.4 ms at 1,000 changes where it took 0.04.

## Environment

- **Machine.** Apple M4 Pro, 12 cores (8 performance, 4 efficiency), 24 GB, macOS 26.6.2 (25G83), the same
  machine as the records of 2026-10-06.
- **.NET.** SDK 10.0.203 through `nix develop`, runtime 10.0.7, CoreCLR, workstation GC, Release,
  `DOTNET_TieredCompilation=0` for every CoreCLR run.
- **Browser.** Google Chrome 154.0.8037.98 through Playwright 1.63.0 (Node 24.14.1), `--project=chrome`,
  headless (`EXGRID_HEADLESS=1`, as this Mac's layer-3 runs are), viewport 1400 × 1100. Headless Chrome renders
  in software: the browser numbers are read against each other and against CoreCLR's, never as what a person
  on real hardware sees.
- **Builds.** The WebAssembly DemoHost, the Server host and the demo API server published with
  `dotnet publish -c Release` (no AOT) and served as CI's full run serves them (`EXGRID_HOSTS`), on ports
  5799 (WebAssembly) and 5798/6798/7798/8798 (Server), each checked free first; only this run's processes
  were stopped. The before code's hosts were published the same way from a `git archive` of `41c8d8c8` with
  the records' harnesses applied, and its CoreCLR harnesses built from the same tree. The four assemblies
  were checked to be the two codes (after: `NextCube` present, `_paints` absent; before: the reverse).
- **Background load.** Load average 1.2–4.2 before the timed batches, higher right after the browser runs
  (up to 11.7, the runs' own Chrome and hosts). An Android emulator, a virtual machine and Docker ran
  throughout, as on 2026-10-06. See "Contention log".

## Method

- **Ticket 01's method, unchanged where the code allows**: each step alone on the same inputs, the least of
  15 runs after 3 untimed, a full collection before each, median and most kept beside the least in every raw
  file; and the whole update as the product runs it, to check the steps against. CoreCLR results go to fixed
  paths in `raw/`.
- **The steps follow the new redraw** (`src/ExPivot/Components/ExPivot.Asking.cs` at `526f3b75`): the fold;
  the answer to a question naming the version on screen (`PivotQuery.WithChangedSince`), which names the
  changed leaves; `PivotEngine.NextCube` from the cube before; `PivotEngine.NextReport` from the report before;
  `HasSameRowsAsAsync`; `ChangesSinceAsync`, which the Change Highlight now records from; `LabelWidthsAsync`,
  which a report made from the one on screen skips; `Show`, with the history's `Record`; the grid's take-in,
  which a vouched report does not walk; the render, which checks the painted rows' keys. Beside them, on the
  same inputs, the steps the redraw ran before and now runs only when it starts afresh (`CubeAsync`,
  `ReportAsync`, `LabelWidthsAsync`, the grid's key check), and `NextCube` with the engine comparing leaves.
  `NextCube` and `NextReport` are timed on a chain of their own, made from a second source as ExPivot makes
  it, so that each is timed on the last link: the cube and report before it and its answer.
- **The collector** by the paint-text record's method, ported unchanged in what it reads: one redraw at a
  time, scrolled to report row 1,000, a full collection before each, every collection inside it read from the
  runtime's GC events (`GcRecorder`, an in-process `EventListener`); and 60 to 150 consecutive redraws with no
  forced collection (H3). The before code ran the same through the paint-text record's own `PaintCosts`.
- **Memory**: the out-of-memory record's loop (`harness/scripts/loop.mjs`, its Playwright path moved to this
  worktree) on `/pivot-live-costs`, a full compacting collection after each redraw, the managed heap, the
  WebAssembly heap, and weak references to every report, cube and Snapshot seen; and ticket 01's CoreCLR
  `pivot-memory`, its loop's own hold on the report before now let go (ticket 01's loop held one).
- **The browser**: ticket 01's `measure-live-costs.spec.mjs` and pages, with three additions: the pivot probe
  also times `NextCube` and `NextReport` on the report the previous probe saw; `/grid-live-local?push=1`
  pushes the source's Window to the grid itself, under the same Row Key, with `&vouch=1` for
  `VouchesDistinctRows`; and two 80-redraw runs, long enough to cross the redraw a compaction lays out afresh.
- **PV-21 beside it**: `measure-pivot.spec.mjs`'s "1,000 changes folded into a million trades on
  `/pivot-live`", after and with the before code's hosts, the same day.

## ExPivot: a live redraw (PV-48)

### CoreCLR, step by step

Milliseconds, before → after, the least of 15 runs. Before is ticket 01's (k = 1,000 at 101,001 and 401,001
rows from its reruns, as its tables take them); after is `raw/pivot-1000x*.json`.

| Step | 10,001 rows, k = 1 | 10,001, k = 1,000 | 101,001, k = 1 | 101,001, k = 1,000 | 401,001, k = 1 | 401,001, k = 1,000 |
|---|---|---|---|---|---|---|
| The source folds the batch (`SnapshotPivotSource.Apply`) | 0.009 → 0.009 | 0.629 → 0.394 | 0.071 → 0.074 | 0.864 → 0.925 | 0.230 → 0.221 | 1.34 → 1.40 |
| The source answers (after: naming the changed leaves) | 0.207 → 0.213 | 0.258 → 0.269 | 2.25 → 2.29 | 2.34 → 2.37 | 9.59 → 9.35 | 9.64 → 9.65 |
| The cube: `CubeAsync` → `NextCubeAsync`, leaves named | 1.06 → **0.209** | 1.10 → **0.538** | 18.4 → **2.31** | 17.6 → **4.89** | 80.1 → **9.62** | 81.4 → **19.4** |
| The report: `ReportAsync` → `NextReportAsync` | 1.30 → **0.046** | 1.36 → **0.572** | 31.6 → **0.393** | 30.9 → **1.42** | 157.2 → **1.55** | 159.1 → **3.12** |
| `HasSameRowsAsAsync` | 0.088 → 0.000 | 0.093 → 0.000 | 1.43 → 0.003 | 1.32 → 0.004 | 4.03 → 0.005 | 4.35 → 0.004 |
| `ChangesSinceAsync` (new: the Change Highlight's comparison) | — → 0.001 | — → 0.001 | — → 0.006 | — → 0.006 | — → 0.006 | — → 0.007 |
| `LabelWidthsAsync` (after: skipped, the report was made from the one on screen, 15 of 15) | 0.321 → 0 | 0.321 → 0 | 3.30 → 0 | 3.23 → 0 | 13.0 → 0 | 12.5 → 0 |
| `Show` (after: with the history's `Record`) | 0.014 → 0.004 | 0.021 → 0.071 | 0.035 → 0.023 | 0.033 → 0.299 | 0.039 → 0.033 | 0.038 → 0.400 |
| The grid checks the report's keys (after: vouched, not run) | 0.144 → 0 | 0.146 → 0 | 1.63 → 0 | 1.74 → 0 | 8.21 → 0 | 8.11 → 0 |
| The grid takes the report in (`ApplyState`, alone) | 0.149 → 0.003 | 0.158 → 0.003 | 1.61 → 0.012 | 1.73 → 0.010 | 8.36 → 0.017 | 8.42 → 0.016 |
| Render (after: with the painted rows' keys checked) | 0.281 → 0.053 | 0.333 → 0.078 | 2.11 → 0.145 | 2.11 → 0.148 | 8.84 → 0.160 | 9.26 → 0.169 |
| **Whole redraw** (`Apply` on the renderer's context, slicing off) | **3.53 → 0.481** | **4.32 → 1.93** | **60.2 → 4.81** | **62.9 → 9.03** | **268.5 → 19.0** | **276.0 → 28.8** |
| Whole redraw, median | 3.93 → 0.503 | 4.69 → 2.11 | 61.6 → 5.15 | 64.4 → 9.65 | 280.1 → 20.2 | 282.6 → 31.0 |
| Whole redraw, most | 4.32 → 0.572 | 4.75 → 2.29 | 76.6 → 5.71 | 73.1 → 10.4 | 301.0 → 21.3 | 296.7 → 32.6 |
| Whole redraw, the before code the same day | 3.60 | 4.42 | 62.6 | 66.7 | 270.4 | 291.6 |
| Whole redraw, after, a second round (`*-r2.json`) | 0.546 | 2.22 | 5.20 | 9.32 | 19.9 | 29.9 |
| Painted rows rendered per redraw, of 11 (median; most) | 11 → 0; 0 | 11 → 3; 4 | 11 → 0; 0 | 11 → 1; 2 | 11 → 0; 0 | 11 → 1; 1 |
| Report rows shared with the report before (median) | — → 9,998 | — → 8,342 | — → 100,998 | — → 99,366 | — → 400,998 | — → 399,367 |
| Allocated per redraw, MB (median) | — → 1.0 | — → 2.3 | — → 9.4 | — → 11.2 | — → 37.2 | — → 39.3 |

Beside it, after, on the same inputs (least of 15; not run in a redraw that is made from the last):

| | 10,001, k = 1 | 10,001, k = 1,000 | 101,001, k = 1 | 101,001, k = 1,000 | 401,001, k = 1 | 401,001, k = 1,000 |
|---|---|---|---|---|---|---|
| `NextCubeAsync`, the engine comparing the leaves (no leaves named) | 0.357 | 0.742 | 3.88 | 7.08 | 16.3 | 28.8 |
| The cube built afresh (`CubeAsync`) | 1.03 | 1.02 | 21.3 | 16.3 | 96.8 (78.3 with k = 1,000 run first) | 77.7 |
| The report laid out afresh (`ReportAsync`) | 1.25 | 1.22 | 33.0 | 33.8 | 151.7 | 152.6 |
| `ChangesSince` over two reports laid out afresh (a data redraw that starts afresh; second round) | 1.64 | 2.07 | 17.7 | 19.9 | 76.8 | 80.5 |
| `LabelWidthsAsync` | 0.293 | 0.307 | 2.95 | 2.96 | 11.9 | 12.5 |
| The grid's key check (`RequireDistinctKeys`) | 0.176 | 0.187 | 2.02 | 2.03 | 8.96 | 9.38 |
| … by instance (`RequireDistinctRows`) | 0.154 | 0.152 | 2.44 | 2.46 | 10.4 | 15.5 |

- **The dominant steps now.** At one change: the answer and the next cube, near equal (101,001 rows: 2.29
  and 2.31 of 4.81 ms; 401,001: 9.35 and 9.62 of 19.0). At 1,000 changes: the next cube (4.89 of 9.03;
  19.4 of 28.8), then the answer. The report is 8–30% of a redraw (it was 31–59%). The steps' least values
  add up to 100–118% of the whole redraw's least (ticket 01: 93–102%): timed alone, each step pays its own
  setting up.
- **Sharing works as ADR-0161 says**: the next report shares every row whose path did not change (400,998 of
  401,001 at one change), the label widths are taken from the report on screen in 15 of 15 redraws, and the
  grid neither walks the report (`_windowVouched` true) nor renders a painted row whose text did not change:
  0 of 11 at one change, where 11 of 11 rendered before; at 1,000 changes exactly the rows whose painted
  value changed (PV-45's count, seen here as M3's).
- **The engine comparing the leaves** (an answer that names none) costs `NextCube` 1.4–1.7 times the named
  path; at 401,001 rows 16.3 against 9.62 ms, against 80.1 for a fresh cube. ADR-0161 measured 12.2 against
  7.6 ms at 400,000 leaves (two Sums, its own layout).
- **The answer step varied between rounds.** Rounds 2 and 3 (`*-r2.json`, `*-r3.json`, a harness build with
  one more measurement after it) read the answer alone at 4.0–4.3 ms at 101,001 rows and 16.9–17.3 at
  401,001, with tight spreads, where round 1, the ordering check (`pivot-1000x400-order.json`) and the before
  code read 2.3 and 9.4–9.9. The whole redraw, which contains the same answer, did not move (5.04–5.20 and
  19.9–20.0 in rounds 2 and 3). Not explained; round 1 stands in the table.
- **Order.** The first configuration of a run reads the fresh cube slower: at 401,001 rows `CubeAsync` was
  96.8 ms with k = 1 first (rounds 1 and 2) and 78.3 with k = 1,000 first, against 77.8 for the before code;
  `NextCube` read 9.62 and 8.45. The whole redraw did not move with the order (19.0 and 21.1).

### The collector

**One redraw at a time**, a full collection before each, 15 redraws after 3 untimed, scrolled to report row
1,000, one change a redraw; every collection inside a redraw read from the runtime's GC events. Before is the
before code the same day (`raw/before-pivot-gc-*.json`); the paint-text record's V0 at 101,001 rows read
70.3 / 72.9 / 84.0 ms with a median pause of 18.1 ms and 82.6 MB allocated a redraw.

| | 10,001 rows | 101,001 | 401,001 |
|---|---|---|---|
| Redraw, least / median / most: before → after | 4.00 / 4.37 / 4.53 → 0.51 / 0.54 / 0.61 | 63.6 / 68.8 / 77.1 → 4.93 / 5.07 / 7.31 | 274.0 / 289.6 / 307.6 → 19.6 / 20.3 / 22.5 |
| The collector's pause per redraw, median (most): before → after | 0 → 0 | 17.8 (27.5) → 0 | 68.8 (79.4) → 0 |
| Collections inside the 15 redraws, gen 0 / gen 1 / gen 2 (of them compacting): before → after | 0 → 0 | 43 / 32 / 0 (10) → 0 | 134 / 136 / 0 (30) → 0 |
| Allocated per redraw, after | 0.99 MB | 9.43 MB | 37.2 MB |

**Steady state** (H3's method): consecutive live redraws, one change each, no forced collection anywhere.
Before is the paint-text record's V0 run of 150 redraws (`core-steady150-1-v0.json`); after is
`raw/pivot-steady-1000x100.json`. "Heap" is the heap after the segment's last collection, not after a full one.

| Redraws, 101,001 rows | Before: median / mean / most, ms | Before: pause, ms in all (per redraw) | Before: heap | After: median / mean / most, ms | After: pause, ms in all (per redraw) | After: heap |
|---|---|---|---|---|---|---|
| 11–60 | 77.2 / 87.3 / 191.7 | 1,293.8 (25.9) | 3,961 MB | 5.67 / 6.53 / 25.0 | 11.7 (0.23) | 234 MB |
| 61–70 | 82.1 / 82.2 / 96.9 | 274.2 (27.4) | 4,758 MB | 5.59 / 13.45 / 85.2 | 14.1 (1.41) | 267 MB |
| 71–100 | 83.2 / 105.4 / 207.2 | 917.1 (30.6) | 5,778 MB | 5.11 / 6.32 / 38.6 | 34.3 (1.14) | 221 MB |
| 101–150 | 85.0 / 97.4 / 205.6 | 1,521.6 (30.4) | 8,088 MB | 5.24 / 7.01 / 86.5 | 20.2 (0.40) | 274 MB |
| **11–150** | **81.9 / 94.4 / 207.2** | **4,006.7 (28.6)** | | **5.37 / 7.15 / 86.5** | **80.3 (0.57)** | |

- **The collector's cost per redraw falls from 28.6 to 0.57 ms** on average, and the redraw's median from 82
  to 5.4 ms. Before, 405 gen-0, 329 gen-1 and 4 gen-2 collections ran in redraws 11–150; after, 6, 6 and 14
  (13 of the gen-2 background).
- **The slowest redraws after** are the two laid out afresh (below): 85.2 ms at redraw 65 (5 collections,
  13.5 ms of pause) and 86.5 ms at redraw 129 (a background gen-2 and five blocking collections, 15.5 ms of
  pause); then 38.6 ms at redraw 76 (a blocking gen-2, 33.3 ms). Before, five redraws in a row took 180–207 ms
  whenever a gen-2 collection came.
- **At 10,001 and 401,001 rows** (70 redraws each, `raw/pivot-steady-1000x*-70.json`), redraws 11–70: median
  0.589 and 22.6 ms, pause 0.13 and 1.26 ms per redraw, the heap after the last collection 26 and 906 MB.
  The 401,001-row run's redraw 65 took 394.5 ms, with 18 collections and 65.7 ms of pause.

**One redraw in 64 is laid out afresh.** In the runs that record it, redraw 65 was the only one of 70 or 80
not made from the report before (`madeFrom: false` in `raw/pivot-memory-1000x100-80-madefrom.json` and
`raw/pivot-steady-1000x*-70.json`); over 200 redraws the heap shows the next at redraw 129. Every run here
applies the same batches (one record, the same seed), so the steady 150-redraw run's redraws 65 and 129 are
these two. A Snapshot compacts once the slices its batches made pile up
(`SnapshotChange.Compacted`), and ADR-0161 starts afresh after a compaction; that this compaction is the
cause is read from the code, not traced. Such a redraw makes a whole generation and compares every row for
the Change Highlight (ADR-0161, "a redraw laid out afresh under data compares every row"): 85.2 ms at 101,001
rows and 394.5 ms at 401,001 on CoreCLR, against 60.2 and 268.5 for every redraw before.

### The browser (WebAssembly)

Apply to the painted frame that shows the new report, ms, least / median / most of 15
(`raw/browser-results.json`). Default slicing (30 ms), as a Consumer runs it; "Applied" includes the redraw's
first slice.

| Report rows | Changes | Before: Apply → painted | **After** | The before code the same day | After: Applied | After: longest task, median / most (before) |
|---|---|---|---|---|---|---|
| 10,001 | 1 | 57 / 78 / 81 | **28 / 29 / 30** | 55 / 78 / 81 | 16.6 / 20.1 / 25.2 | 0 / 0 (51) |
| 10,001 | 1,000 | 68 / 71 / 94 | **39 / 46 / 50** | — | 25.0 / 32.5 / 37.7 | 0 / 0 (59) |
| 101,001 | 1 | 529 / 563 / 692 | **143 / 152 / 164** | 534 / 548 / 673 | 31.4 / 32.1 / 32.9 | 0 / 0 (170) |
| 101,001 | 1,000 | 555 / 567 / 672 | **175 / 183 / 198** | — | 41.7 / 43.6 / 47.6 | 54 / 58 (132) |
| 401,001 | 1 | 2,338 (one redraw; the runs ran out of memory) | **532 / 537 / 581** | — | 35.3 / 36.2 / 71.7 | 0 / 75 (75) |
| 401,001 | 1,000 | out of memory | **600 / 610 / 651** | — | 44.4 / 49.4 / 81.0 | 57 / 86 (—) |
| 101,001, 80 redraws | 1 | — | 143 / 153 / **823** (redraw 65) | — | | 0 / 57 |
| 401,001, 80 redraws | 1 | — | 532 / 546 / **3,329** (redraw 65) | — | | 0 / 81 |

The steps the page can time, before each batch (`?probe=1`; least / median of 15). `NextCube` and
`NextReport` remake the report on screen from the one the previous probe saw, with the answer the source
holds (it names one leaf):

| | 10,001 rows | 101,001 | 401,001 |
|---|---|---|---|
| `NextCube` (after; CoreCLR 0.209 / 2.31 / 9.62) | 1.8 / 3.6 | 29.2 / 31.1 | 88.2 / 95.0 |
| `NextReport` (after; CoreCLR 0.046 / 0.393 / 1.55) | 0.8 / 1.4 | 6.9 / 7.3 | 22.8 / 23.6 |
| `PivotEngine.Cube` afresh: before → after | 18.6 / 22.1 → 12.0 / 19.2 | 136 / 142 → 118.5 / 134.8 | — → 463 / 524 |
| `PivotEngine.Report` afresh: before → after | 13.5 / 17.2 → 12.2 / 15.6 | 192 / 194 → 185 / 201 | — → 923 / 933 |
| The key check: before → after | 1.4 / 1.7 → 2.0 / 2.3 | 14.3 / 14.5 → 19.0 / 19.4 | — → 79.0 / 80.0 |
| The instance check: before → after | 0.9 / 1.1 → 1.2 / 1.4 | 9.0 / 9.2 → 12.7 / 13.0 | — → 50.5 / 51.5 |
| Apply → painted in the probe runs: before → after | 51 / 64 → 13 / 15 | 526 / 548 → 132 / 133 | — → 524 / 532 |

- **A live report of 401,001 rows now lands in about 0.54 s** where one redraw took 2.3 s and longer runs
  ran out of memory; 101,001 rows in 0.15 s where they took 0.55 s. The same-day run of the before code
  repeated 2026-10-06's figures within 15 ms at the median (78 and 548 against 78 and 563).
- **The browser keeps less of the gain than CoreCLR.** The redraw is 3.7–4.4 times faster in the browser
  (101,001 and 401,001 rows, one change) against 12.5–14 on CoreCLR. `NextCube` and `NextReport` in the page
  cost 9–13 and 15–18 times their CoreCLR time, where a whole fresh redraw cost about 9 times; at 401,001 rows
  they are 111 of the 532 ms. The other ~420 ms — the source's answer and its fold of the deferred batch, the comparisons,
  the turns PV-40's slices yield, Mono's collector, the render — were not separated here.
- **The probe made the small report faster** (13 / 15 ms against 28 / 29 at 10,001 rows), as it did before
  (51 / 64 against 57 / 78): the probe's own work runs before Apply, after the previous redraw's tail.
- **The redraw a compaction lays out afresh is the slow one**: 823 ms at 101,001 rows and 3,329 ms at
  401,001, redraw 65 of both 80-redraw runs, against 529–692 and 2,338 for every redraw before.

## LV-23: ExPivot's memory under live redraws

**Browser**, the out-of-memory record's loop (`raw/oom-*.log`): the managed heap after a full, compacting
collection after each redraw, the WebAssembly heap, and how many of the reports seen are alive.

| Run | After loading | After redraw 1 / 2 / 3 / 4 | Redraw 5 to the last | Reports alive at the end | WebAssembly heap | Out of memory |
|---|---|---|---|---|---|---|
| Before, 101,001 rows, 12 redraws (2026-10-06, `run2-1000x100-gc.log`) | 87.9 MB | 129.1 / 172.3 / 215.5 / 258.7 | +41.1 MB a redraw, 587.9 at 12 | 13 of 13 | 199 → 984 MB, rising | not in 12; the 401,001 run below did |
| **After, 101,001 rows, 20 redraws** | 88.3 MB | 89.6 / 89.5 / 89.5 / 89.5 | **89.5 MB, flat** | **1 of 21** (the one on screen) | 166 → 199 MB, flat from redraw 1 | no |
| Before, 401,001 rows (2026-10-06, `run1-1000x400.log`, no collection forced) | — | — | WebAssembly heap 591 → 2,048 MB | — | — | **on the 7th redraw** |
| **After, 401,001 rows, 15 redraws** | 325.2 MB | 330.2 / 329.7 / 329.7 / 329.7 | **329.7 MB, flat** | **1 of 16** | 591 MB, flat | no |
| After, 401,001 rows, 80 redraws | 325.2 MB | 330.2 / 329.7 / … | 329.7 MB, except 402.7 after redraw 65 | 1 of 81 | 591 → 786 MB at redraw 65, then flat | no |

- **The out-of-memory is gone at both sizes.** One report is alive after every redraw, the one on screen;
  the Snapshots stop at 4, as `AnswersHeld = 4` intends.
- **The redraw laid out afresh raises the WebAssembly heap for good**: at 401,001 rows the managed heap was
  402.7 MB after redraw 65 and 330.2 MB after redraw 66, but the WebAssembly heap, which never shrinks, grew
  from 591 to 786 MB and stayed there.

**CoreCLR**, ticket 01's `pivot-memory` (one change a redraw, a full collection after each; the loop's own
hold on the report before let go): no earlier report or cube alive after any redraw, at 101,001 rows over 20
redraws (152.5 → 154.3 → 157.1 MB) and at 401,001 over 15 (399.9 → 406.7 → 408.2 MB). Ticket 01 measured 154
→ 1,225 MB over 20 redraws at 101,001 rows, every report alive. Over 200 redraws at 101,001 rows
(`raw/pivot-memory-1000x100-200.json`) the heap saws between 154 and 171 MB, with readings of 227.1 and
225.9 MB right after the two redraws laid out afresh (65 and 129), back under 171 MB two redraws later.

## ExGrid: `GridSource.From` with a Row Key (`/grid-live-local`)

### CoreCLR, step by step

Milliseconds, before → after, the least of 15 runs (`raw/grid-1e5.json`, `raw/grid-1e6.json`).

| Step | 10⁵, k = 1 | 10⁵, k = 100 | 10⁵, k = 1,000 | 10⁶, k = 1 | 10⁶, k = 100 | 10⁶, k = 1,000 |
|---|---|---|---|---|---|---|
| Fold (`Apply`, gathering) | 0.008 → 0.007 | 0.077 → 0.075 | 0.570 → 0.587 | 0.028 → 0.030 | 0.156 → 0.169 | 1.01 → 1.02 |
| Publication (`PublishGathered`) | 0.051 → 0.050 | 0.191 → 0.189 | 1.22 → 1.20 | 0.390 → 0.393 | 0.606 → 0.621 | 2.23 → 2.17 |
| &nbsp;&nbsp;of which the incremental requery (`LiveRequery.Apply`, alone) | 0.045 → 0.042 | 0.075 → 0.073 | 0.356 → 0.396 | 0.372 → 0.370 | 0.469 → 0.498 | 1.10 → 1.15 |
| &nbsp;&nbsp;of which the Change Highlight's record (alone) | 0.007 → 0.006 | 0.071 → 0.067 | 0.631 → 0.601 | 0.026 → 0.018 | 0.103 → 0.108 | 0.720 → 0.727 |
| The grid takes the Window in (`ApplyState`) | 0.014 → 0.005 | 0.007 → 0.007 | 0.010 → 0.010 | 0.103 → 0.017 | 0.017 → 0.017 | 0.017 → 0.015 |
| &nbsp;&nbsp;the Selection Summary's walk when only the last row changed (alone) | 0.488 → **0.004** | 0.484 → **0.004** | 0.520 → **0.007** | 8.62 → **0.011** | 8.92 → **0.010** | 11.1 → **0.010** |
| Render (`StateHasChanged`) | 0.062 → 0.051 | 0.079 → 0.064 | 0.118 → 0.096 | 0.103 → 0.115 | 0.125 → 0.128 | 0.154 → 0.159 |
| **Whole update** | 0.100 → 0.085 | 0.290 → 0.283 | 2.02 → 1.96 | 0.745 → 0.562 | 0.922 → 0.857 | 3.34 → 3.31 |
| Whole update, median | 0.279 → 0.094 | 0.309 → 0.295 | 2.11 → 2.06 | **3.07 → 0.807** | 1.16 → 1.18 | 3.68 → 3.63 |
| Whole update, most | 0.663 → 0.160 | 0.449 → 0.370 | 2.23 → 2.23 | **10.1 → 0.933** | 1.53 → 1.35 | 3.86 → 3.82 |
| Painted rows rendered per update, of 18 (median) | 0 → 0 | 1 → 1 | 9 → 9 | 0 → 0 | 3 → 3 | 11 → 11 |
| Render batch per update, estimated bytes (median) | 56 → 56 | 464 → 464 | 1,471 → 1,471 | 56 → 56 | 586 → 586 | 1,718 → 1,718 |

- **The Selection Summary's walk is gone** (ADR-0130's note of 2026-10-07, ticket 08): with no question
  standing, nothing is walked, so a one-row update to the end of a million rows costs what one to the top
  does. That is the whole change at one change a batch: median 3.07 → 0.807 ms, most 10.1 → 0.933.
- **Everything else is as it was** within run-to-run spread: the source's fold and publication, the render,
  the rows rendered and the bytes. The vouch was already in place for a bundled source (LV-10): 0 key calls in
  `ApplyState` over a new Window of 100,000 rows, and 100,000 under a Row Key of the grid's own
  (`raw/vouch.json`), as before.

### The browser (WebAssembly)

Apply to the painted frame, ms, least / median / most of 15 (`raw/browser-results.json`).

| | 10⁵, k = 1 | 10⁵, k = 100 | 10⁵, k = 1,000 | 10⁶, k = 1 | 10⁶, k = 100 | 10⁶, k = 1,000 |
|---|---|---|---|---|---|---|
| Before | 3.2 / 16.0 / 25.2 | 6.4 / 10.1 / 24.3 | 18.0 / 25.2 / 32.6 | 5.0 / 91.4 / 136 | 7.9 / 13.3 / 79.3 | 22.7 / 28.6 / 87.3 |
| **After** | **1.6 / 3.1 / 12.5** | 5.6 / 9.4 / 17.5 | 18.0 / 24.8 / 34.7 | **3.4 / 6.8 / 72.7** | 6.8 / 11.4 / 73.7 | 22.4 / 28.5 / 87.4 |
| Runs where the grid's DOM changed, after | 0 of 15 | 12 of 15 | 15 of 15 | 0 of 15 | 13 of 15 | 15 of 15 |
| The frame's style, layout and paint, after | 0.4–1.0 | 0.2–3.4 | 0.9–1.9 | 0.4–1.0 | 0.7–2.3 | 0.9–1.4 |
| The source alone, the same batch (`?probe=1`) | | | | | | 14.9 / 15.4 → 14.2 / 15.1 |

- **LV-15**: 1,000 changes to 10⁶ rows, Apply to the painted frame, **22.4 ms least, 28.5 median, 87.4 most**,
  against PV-21's 0.2 s (before 22.7 / 28.6 / 87.3).
- **A one-row update to a million rows** no longer pays the walk: median 91.4 → 6.8 ms. The most, 72.7 ms,
  is one long task in a run of 15, as at 100 and 1,000 changes (73.7 and 87.4), and as before (79.3, 87.3).

## A pushed Window of 10⁶ rows (ticket 07's open half)

`/grid-live-local?push=1`: the page pushes the source's Window (all 10⁶ rows) to the grid itself, with the
trade id as the Row Key, re-rendering on each change of the source; `&vouch=1` passes
`VouchesDistinctRows`. One batch at a time, interval 0. Least / median / most of 15, ms.

| | k = 1,000 | k = 1 |
|---|---|---|
| **Not vouched**: Apply → painted | 590 / 632 / 764 | 640 / 645 / 676 |
| &nbsp;&nbsp;Applied (the page's render and the grid's take-in, with its check, in that task) | 283 / 299 / 382 | 320 / 324 / 354 |
| &nbsp;&nbsp;from the frame to the paint (a second long task, after the frame) | 294 / 345 / 385 | 319 / 322 / 328 |
| **Vouched**: Apply → painted | **23.7 / 29.7 / 84.4** | **4.3 / 7.3 / 67.7** |
| The same update through the bound source (above) | 22.4 / 28.5 / 87.4 | 3.4 / 6.8 / 72.7 |
| The check by key alone over the 10⁶ rows (`?probe=1`): before → after | 269 / 278 → 267 / 280 | |

On CoreCLR (`raw/grid-1e6.json`), the take-in of a pushed 10⁶-row Window under a Row Key: 47.1–48.1 ms
unvouched (before 41.3–47.1; the before code the same day 43.4–48.9), **0.011–0.015 ms vouched**, and
0.100–0.132 ms with the render that checks the 18 painted rows' keys. Counted on CoreCLR
(`raw/push-count.json`), an unvouched pushed update asks 100,018 keys over a 100,000-row Window — one pass and
the 18 painted rows — and a vouched one 18.

- **Vouched, a pushed million-row Window updates as fast as the bound source does** (29.7 against 28.5 ms at
  the median for 1,000 changes). Unvouched it takes 0.6 s to paint, three times PV-21's 0.2 s.
- **The unvouched update is two long tasks**: the one that applies the batch (283–382 ms, nearly all the
  check) and a second of about 300 ms right after the frame, before the paint. The count shows the Window is
  checked once per update, so the second task is not a second check; what it is was not measured (Mono's
  collector after the check's million-entry dictionary is a guess, not a finding).

## The bytes per update on the Server host

Median (range) of 15 presses at 10⁶ rows (`raw/server-bytes.json`, derived from the per-connection counts as
ticket 01 derived its own: the harness's pick of the connection names the polling one here too, and is not
read). The WebSocket's per-message compression on.

| Changes | The update's render batch: before → after | On the wire: before → after | The un-mark a second later, batch / wire (after) | Per update in all, wire (mean): before → after |
|---|---|---|---|---|
| 1 | 73 → 73 | 19 (19–23) → 19 (19–23) | none | 20.0 → 20.0 |
| 100 | 644 (73–1,159) → 644 (73–1,159) | 71 (21–221) → 79 (21–223) | 301 / 24 | 113.1 → 113.9 |
| 1,000 | 2,562 (1,965–3,482) → 2,562 (1,965–3,482) | 297 (216–428) → 296 (216–429) | 1,293 / 133 | 424.3 → 423.7 |

The same render batches to the byte, as they should be: the bundled source's path did not change, and only
painted rows travel. 10⁵ rows were not run again (ticket 01 found them equal to 10⁶ within a few bytes).

## LV-15's parts

| Part | Before | After |
|---|---|---|
| 1,000 changes to 10⁶ rows on `/grid-live-local`, Apply to the frame, in the browser (published, no AOT), against PV-21's 0.2 s | 22.7 / 28.6 / 87.3 ms | **22.4 / 28.5 / 87.4 ms**; 1 change 3.4 / 6.8 / 72.7; 100 changes 6.8 / 11.4 / 73.7 |
| The grid's pass over a report's Row Keys against its pass over the instances, at 10⁴, 10⁵ and 4×10⁵ report rows | CoreCLR 0.144 / 1.63 / 8.21 against 0.127 / 2.66 / 9.83 ms; browser 1.4 / 14.3 / — against 0.9 / 9.0 / — | **Not run in a redraw: ExPivot vouches for its report.** Alone, CoreCLR 0.176 / 2.02 / 8.96 against 0.154 / 2.44 / 10.4 ms; browser 2.0 / 19.0 / 79.0 against 1.2 / 12.7 / 50.5 |
| The bytes per live update on the Server host, 1 / 100 / 1,000 changes | render batch 73 / 641–644 / 2,562 bytes, 19 / 71 / 297 on the wire, an un-mark batch 0 / 301 / 1,293 | the same: 73 / 644 / 2,562, 19 / 79 / 296 on the wire, un-mark 0 / 301 / 1,293 |
| The requery and the grid's pass per update, at 10⁵ and 10⁶ rows | requery 0.045–0.356 and 0.372–1.105 ms; the grid's pass none (vouched), `ApplyState` 0.007–0.017 ms plus the Selection Summary's walk, up to 0.52 and 11.1 ms | requery 0.042–0.396 and 0.370–1.15 ms; the grid's pass none; `ApplyState` 0.005–0.017 ms, **the walk 0.004–0.011 ms** |

## PV-21 beside PV-48

PV-21's targets, in the browser over 1,000,000 records: 1,000 changes ≤ 0.2 s; the page blocked ≤ 50 ms at a
time. Its own measurement, "1,000 changes folded into a million trades on `/pivot-live`"
(`measure-pivot.spec.mjs`, 12 batches, Apply to the report's frame, least / median / most):

| | Apply → the report's frame | Longest task |
|---|---|---|
| Before code, the same day (`raw/browser-before-pv21-live.log`) | 23 / 27 / 38 ms | 0 / 0 / 0 |
| **After** (`raw/browser-pv21-live-after.log`) | **22 / 28 / 42 ms** | 0 / 0 / 0 |
| For reference: Linux, 2026-10-01 (`2026-10-01-linux-measure`) | 35 / 43 / 57 ms | 0 / 0 / 164 |

`/pivot-live`'s report is small (its million trades fold into a few thousand leaves), so ADR-0161 does not
move it; both stay well inside 0.2 s. Beside the D10 reports above: at 101,001 report rows a redraw is now
inside 0.2 s (143–198 ms); at 401,001 rows it is 0.53–0.65 s, and its longest task reaches 75–86 ms, past
the 50 ms PV-21 allows.

## What got slower

Measured, not explained away.

- **A data redraw laid out afresh.** One redraw in 64 in these runs (redraws 65 and 129, one change a batch)
  was not made from the report before. On CoreCLR it took 85.2 ms at 101,001 rows and 394.5 ms at 401,001 (with 13.5 and 65.7
  ms of collector pause), where every redraw took 60.2 and 268.5 ms before, the least of 15. In the browser,
  823 and 3,329 ms, where every redraw took 529–692 and 2,338 ms. It compares every row for the Change
  Highlight, as ADR-0161 decided: `ChangesSince` over two reports laid out afresh costs 17.7–19.9 ms at
  101,001 rows and 76.8–80.5 at 401,001 on CoreCLR, and it builds the label widths again (12–13 ms at
  401,001). At 401,001 rows in the browser it also raised the WebAssembly heap from 591 to 786 MB, for good.
- **A pushed Window's check by key, when it is not vouched**, on CoreCLR: 47.2–48.3 ms at 10⁶ rows against
  43.7–44.2 for the before code the same day (+8–10%) and 39.9–42.0 on 2026-10-06; 3.66–3.92 ms at 10⁵
  against 3.19–3.24 the same day (+15–21%). The take-in that runs it, 47.1–48.1 against 43.4–48.9 the same
  day. In the browser the check over 10⁶ rows did not move (267 / 280 against 269 / 278 ms). The check now
  takes its keys through the same method as the painted rows' check (ticket 07).
- **The checks over a report in the browser**, which a redraw no longer runs: by key 14.3 → 19.0 ms and by
  instance 9.0 → 12.7 at 101,001 rows; 1.4 → 2.0 and 0.9 → 1.2 at 10,001. On CoreCLR by key 1.63 → 2.02 and
  8.21 → 8.96 ms. The instance check's code did not change; why it reads slower in the page was not
  investigated.
- **`Show`** at 1,000 changes: 0.033–0.038 → 0.299–0.400 ms (CoreCLR), the history's `Record` of the changed
  cells' times (ADR-0161). Under half a millisecond.
- **The grid's render** under a Row Key, with the painted rows' keys checked on every render (ADR-0141,
  2026-10-07): 0.103 → 0.115 ms at 10⁶ rows and one change; 0.154 → 0.159 at 1,000. Within the spread at the
  other sizes.
- **Not slower, though a first reading said so**: the fresh cube at 401,001 rows read 96.8 ms in the first
  configuration of two runs, and 78.3 with the order reversed, against 77.8 for the before code; the fresh
  report 151.7–159.6 against 156.8–160.8.

## Not measured, and why

- **Where the browser's ExPivot redraw spends the time the probe does not see**: at 401,001 rows `NextCube`
  and `NextReport` are 111 of 532 ms. The answer (which folds the deferred batch when asked), the comparisons,
  the slices' turns, Mono's collector and the render were not timed apart: the redraw runs inside ExPivot
  over several turns, and the page can time only what it can call. Mono's collector pauses per redraw were
  not counted in the browser.
- **The second long task of an unvouched pushed update** (about 300 ms after the frame): not identified.
- **The before code at 401,001 rows in the browser**, at 1,000 changes: it ran out of memory on 2026-10-06,
  and was not run again.
- **The before code's steady state at 10,001 and 401,001 rows on CoreCLR**: at 401,001 rows its kept paints
  would hold up to 64 generations of about 200 MB, beyond this machine; H3's 101,001-row run stands as the
  before.
- **The browser grid runs with the before code the same day**: ticket 01's figures are used. The path they
  measure changed only in the Selection Summary's walk, which the 10⁶-row, one-change run shows.
- **Real hardware, headed, and Windows**: headless Chrome on one Mac only, as ticket 01.
- **ExPivot's bytes on the Server host**, and its memory on a circuit: not run, as in ticket 01.
- **Why the answer step read 1.8 times higher in rounds 2 and 3** with the whole redraw unchanged: not
  investigated.

## Contention log

Every timed batch waited, by a script with a bounded poll of up to an hour, for two consecutive idle checks
30 s apart, was checked every 20 s while it ran and again after it (`raw/contention.log`). The check looked
for the Codex track's worktrees (`~/.codex-workspaces/worktrees/33609b58…`: any dotnet, testhost, node,
Playwright or Chrome process), any other checkout's Playwright runner or Playwright-driven Chrome, any busy
`dotnet`, MSBuild, testhost or compiler server, any `dotnet test`, `build` or `publish`, and the machine-wide
layer-3 lock held by someone else; every browser run also held that lock itself.

- **No timed batch overlapped another track's work.** All 31 CoreCLR runs (12:57–14:21 BST) and all 32
  browser and memory runs (29 layer-3 runs and 3 loops, 13:30–14:05) found the machine idle at their first two
  checks, and none was abandoned.
  The integration worktree's layer-3 run had ended, and its lock was free, when the first batch started.
- **Two flags were false**: the during-run checks of the 401,001-row ExPivot runs (13:34 and 13:35) named a
  Playwright runner, `npm exec playwright test measure-live-costs.spec.mjs`, which was this run's own (its
  command line does not carry the worktree's path). The check was corrected at about 13:36 to tell a runner
  by its working directory, and flagged nothing after.
- **This run's own side work**: a harness build and two untimed counting runs (`push-count`, the 80-redraw
  `pivot-memory`) at 13:52–13:53, during the Server-bytes run at 100 changes, whose byte counts load does not
  move; and a one-second version query at 13:31, during the 10,001-row run at 1,000 changes.
- **Load average** 1.2–4.2 before every batch. Right after the browser runs it read up to 11.7 (13:40–13:42,
  the grid runs back to back): that is the runs' own Chrome, host and million-trade setup, not another track.

## Reproducing

The harness is in [`harness/`](harness/README.md), with how to put it back and run it. In short, from the
repository root with the harness applied:

```sh
# CoreCLR
nix develop -c dotnet build -c Release spikes/live-update/Costs/Costs.csproj
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot  out.json 1000 400 1,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll grid   out.json 1000000 1,100,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot-gc out.json 1000 100 3 15 full verbose
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot-steady out.json 1000 100 150
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot-memory out.json 1000 100 1 20
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll push-count out.json 100000 3

# The browser, against published hosts (from tests/ExGrid.Browser)
EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=1 EXGRID_HOSTS=<hosts> \
  EXGRID_BASE_URL=http://localhost:5799 EXGRID_HEADLESS=1 EXGRID_COSTS_OUT=out.json \
  nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome
EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 EXGRID_COSTS_PUSH=1 EXGRID_COSTS_VOUCH=1 …   # as above
node harness/scripts/loop.mjs http://localhost:5799 1000 100 20 "" 1     # with static-host.mjs serving <hosts>/wasm/wwwroot on 5799
```

The tables were made from the raw files by `harness/scripts/core-tables.py`, `browser-tables.py`,
`server-bytes.py` and `metrics.py` (which writes [`metrics.json`](metrics.json)). Every browser run passed its
fixture's console verdict: zero console errors, zero page errors, no unhandled exception in the host log
(CON-1, CON-2, CON-6).
