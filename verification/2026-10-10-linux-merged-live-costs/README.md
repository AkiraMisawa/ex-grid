# Live costs on the merged code: PV-48, LV-23 and PV-21, against `main` (PR #70)

Date: 2026-10-10. `claude/live-data-best` at `80d6b38` (its `src/` is that of `c82b894`, which changed
only a test) against `main` at `db60f6f`. This is the measurement decided with the user on 2026-10-09 (Q10 of
the grilling of the merged pull request): PV-48 and LV-23 repeated on the merged code, which neither
track's after-record measured ([`2026-10-06-macos-live-report-after`](../2026-10-06-macos-live-report-after/README.md)
measured the Codex track's ExPivot, [`2026-10-07-macos-live-update-costs-after`](../2026-10-07-macos-live-update-costs-after/README.md)
the Claude Code track's). PV-21, which PV-48 records beside it, ran on both sides too. ExGrid's own live
update is measured beside them, to see that the merge cost the grid nothing.

It decides nothing. Performance never gates (AGENTS.md); every number here is observational. The harness
is disposable and was never committed where it builds; a copy is in [`harness/`](harness/), the raw files
are in [`raw/`](raw/), and [`metrics.json`](metrics.json) holds every figure as a spread
(`harness/metrics.py` composes it; `harness/tables.py` prints the tables below from `raw/`).

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Ubuntu 24.04.5, Linux 6.18;
  .NET SDK 10.0.203 through nix, runtime 10.0.7; Chromium 141.0.7390.37 (Playwright's build), headless,
  1,400 × 1,100. The earlier records ran on an Apple M4 Pro: compare their shapes with these, not their
  milliseconds.
- **Both sides** are detached worktrees at their commits with the same harness files added. The one
  difference: the CoreCLR harness waits for main's report as `ExPivot.Report` and its rows, and for the
  branch's as `ExPivot.Report.Metadata`, because the branch's ExPivot holds a report Window, not a report.
  The runs alternate (main, branch, branch, main, …) with nothing else running; the load average stayed
  near 1.0 throughout (`raw/run.log`).
- **CoreCLR**: Release, `DOTNET_TieredCompilation=0`, bUnit, a full collection before each timed run.
  ExPivot: two warm-ups and nine runs a setting, in three rounds. ExGrid: three warm-ups and fifteen runs,
  in two rounds.
- **Browser**: the DemoHost published in Release as WebAssembly, trimmed, without AOT and without the
  `wasm-tools` workload, served as static files. A disposable page, `/perf-live-memory`
  (`harness/PerfLiveMemoryPage.razor.txt`), holds ticket 01's fixture in a `PivotSource.From`: 1,000 outer
  Items × *b* inner, one record a leaf, a decimal Sum, 1,000 changed records a batch, `RedrawInterval` 0,
  `ChangeHighlightDuration` one second. Each redraw is a click, timed until the report's viewport shows
  other text; after it, a full collection (`GC.Collect`, `WaitForPendingFinalizers`, `GC.Collect`,
  `GC.GetTotalMemory(true)`) and the size of the WebAssembly heap.
- **PV-21**: the repository's own `measure-pivot.spec.mjs`, unchanged, on each side's published host and its
  Release demo API holding a million trades, headed under xvfb as `verification/2026-10-01-linux-measure`
  ran it. Both sides read the same 89.7 MiB CSV, written by the page's own `DemoCsv.TradeExportFileAsync`
  (`harness/csvgen.*.txt`). One run a side; each gesture three to twelve times within it.

## In brief

- **ExPivot's live update is 3 to 147 times faster than `main`'s.** At 401,001 report rows, round
  medians: 3.9–4.3 ms against 623–643 at one change, 14–22 ms against 625–650 at 1,000. `main`
  allocated 281 MiB an update and paused about 150 ms an update for the collector; the branch
  allocates 1–18 MiB and did not collect.
- **The browser no longer runs out of memory, and its heap levels off.** At 101,001 report rows `main` grew
  by 41.2 MiB a redraw, as the record of 2026-10-06 found; the branch levels off at 137 MiB after six
  redraws. At 401,001 rows `main` ran out of memory on the 7th redraw (+151.5 MiB a redraw, 3.2 s a
  redraw); the branch held 420 MiB over 20 redraws at a median of 96 ms.
- **The first report is slower than `main`'s**: 194–214 ms against 136–144 at 101,001 rows, 903–949
  against 662–826 at 401,001. This is what remains after the fix of 2026-10-09 (5.1 s → 0.8 s at 401,001
  rows; `implementation-status.md`), recorded as it is.
- **A new question over a million trades is slower than `main`'s, by 16 to 60%** (PV-21): a question of
  13,500 combinations takes 371 ms against 298, one of 198,450 2.96 s against 1.85, a million-row CSV's
  first question 412 ms against 332. The extra is the branch's .NET code under the browser's interpreter,
  and some collection: on CoreCLR the same questions are faster than `main`'s at 134,730 combinations
  ("Where a new question's extra time goes"). The live update over a million trades is about even (37 ms
  against 32 for 1,000 changes), and every gesture's first visual answer stays within PV-21's 0.1 s on
  both sides.
- **ExGrid's live update did not change** at 100 and 1,000 changes over 10⁶ rows. At one change the
  branch's median is 6.6–6.8 ms against `main`'s 14.6–15.4 in both rounds; this harness does not separate
  why.

## ExPivot's whole live update on CoreCLR, `main` against the branch (PV-48)

`harness/PerfWholeUpdate.cs.txt`: from the source's `Apply` through the component's render, until the
report's version changes, on ticket 01's fixture (1,000 outer Items × 10, 100 and 400 inner, one record a
leaf, a decimal Sum), as the component takes it: `RedrawInterval` 0, the test clock advanced before each
batch. Rounds 1 and 2 advance it 1 ms, inside the one-second Change Highlight; round 3 advances it 5 s,
past it. Medians of nine runs, round by round.

| Report rows | Changes | `main`, median ms | branch, median ms | `main` / branch | Allocated an update, MiB: `main` → branch | Collector pause an update, ms: `main` → branch |
|---|---|---|---|---|---|---|
| 11,001 | 1 | 13.45 / 12.96 / 13.18 | 2.10 / 2.41 / 2.52 | 5× | 9.4 → 1.0 | 0 → 0 |
| 11,001 | 1,000 | 13.94 / 13.71 / 13.57 | 4.42 / 3.86 / 4.23 | 3× | 9.6 → 2.5 | 0 → 0 |
| 101,001 | 1 | 119.19 / 115.04 / 118.29 | 3.05 / 2.70 / 2.70 | 44× | 76.5 → 1.0 | 0 → 0 |
| 101,001 | 1,000 | 118.88 / 116.48 / 117.56 | 12.49 / 11.77 / 8.52 | 10× | 76.6 → 8.1 | 0 → 0 |
| 401,001 | 1 | 627.09 / 643.44 / 623.45 | 4.26 / 4.27 / 3.89 | 147× | 281.0 → 1.0 | 157 → 0 |
| 401,001 | 1,000 | 629.60 / 624.86 / 649.76 | 22.10 / 22.30 / 14.23 | 28× | 281.2 → 17.8 | 146 → 0 |

The first report, and the live heap after a full collection once a size's 22 updates were done:

| Report rows | First report, ms: `main` | branch | Heap after the updates, MiB: `main` | branch |
|---|---|---|---|---|
| 11,001 | 1,459 / 1,502 / 1,472 | 1,634 / 1,657 / 1,715 | 143 / 143 / 143 | 27 / 27 / 21 |
| 101,001 | 137 / 136 / 144 | 194 / 198 / 214 | 1,400 / 1,400 / 1,407 | 211 / 211 / 148 |
| 401,001 | 826 / 790 / 662 | 903 / 949 / 940 | 6,100 / 6,100 / 6,135 | 814 / 814 / 607 |

- **The 11,001-row first report is mostly compilation**: it is the first report in each process, and
  `DOTNET_TieredCompilation=0` compiles every method fully on first use. The larger sizes come after it.
- **`main`'s heap grows with every update**, by about as much as each update allocates; the records of
  2026-10-06 found every report alive. The branch keeps the reports its Change Highlight compares, within
  ADR-0153's bound: in round 3, where the batches come after the highlight has ended, it keeps fewer (148
  against 211 MiB at 101,001 rows, 607 against 814 at 401,001), and its 1,000-change update is faster.

### Step by step, and the component (the branch)

The record of 2026-10-06's own harnesses, run again on the merged code (`harness/ReportSteps.cs.txt` and
`harness/ReportRendering.cs.txt`; changed only for a renamed parameter, `Source`, and the label-width
step's new argument list). 1,000 changes a batch; least / median of nine, milliseconds.

| Report rows | Snapshot Apply | Fold | Affected Cube | Report structure | Label widths | Row sequence shared |
|---|---|---|---|---|---|---|
| 11,001 | 0.155 / 0.165 | 0.160 / 0.183 | 1.063 / 1.118 | 0.069 / 0.071 | 0.005 / 0.005 | yes |
| 101,001 | 0.184 / 0.195 | 0.459 / 0.575 | 2.798 / 4.856 | 0.114 / 0.122 | 0.007 / 0.008 | yes |
| 401,001 | 0.248 / 0.263 | 1.497 / 1.571 | 6.112 / 6.999 | 0.189 / 0.214 | 0.009 / 0.011 | yes |

| Report rows | Apply → component render, ms (least / median / most) | Painted | Rendered | Text changed | Mounted | Vouched Window admission, ms (least) |
|---|---|---|---|---|---|---|
| 11,001 | 3.30 / 3.53 / 4.42 | 18 | 4 | 4 | 0 | 0.0021 |
| 101,001 | 9.75 / 10.39 / 14.16 | 18 | 2 | 2 | 0 | 0.0036 |
| 401,001 | 18.98 / 20.27 / 21.87 | 18 | 2 | 2 | 0 | 0.0044 |

Every step stays as the Codex track's record found it, in shape: the affected Cube is the largest part,
nothing grows with the rows unchanged, the row sequence is shared, the rows rendered are exactly those
whose painted text changed, and none is mounted again.

## ExGrid's whole live update (no change)

`harness/PerfGrid.*.txt`, a console harness over a counting renderer: `/grid-live-local`'s trades in a
keyed `GridSource.From` of 10⁶ rows, `Apply` on the renderer's context through the grid's render and its
continuations; and a pushed 40-row Window replaced whole, keyed and unkeyed. Least / median / most of 15
(the repaints, of 60), round 1 ; round 2.

| Changes | `main` | branch | Allocated, MiB (median): `main` → branch |
|---|---|---|---|
| 1 | 6.33 / 15.43 / 20.65 ; 5.69 / 14.57 / 19.77 | 4.22 / 6.57 / 8.36 ; 4.30 / 6.79 / 8.40 | 15.3 → 15.3 |
| 100 | 4.80 / 7.47 / 11.63 ; 4.83 / 7.22 / 16.89 | 4.99 / 7.60 / 14.10 ; 4.91 / 7.56 / 12.98 | 15.4 → 15.4 |
| 1,000 | 9.97 / 12.69 / 16.62 ; 10.18 / 12.75 / 15.46 | 10.05 / 12.44 / 14.06 ; 9.86 / 12.55 / 15.01 | 16.5 → 16.5 |
| Repaint, keyed (18 painted) | 0.42 / 0.47 / 0.89 ; 0.27 / 0.33 / 1.19 | 0.39 / 0.44 / 0.56 ; 0.39 / 0.43 / 0.54 | |
| Repaint, unkeyed (18 painted) | 0.39 / 0.47 / 1.75 ; 0.27 / 0.33 / 1.16 | 0.42 / 0.47 / 0.72 ; 0.41 / 0.48 / 0.95 | |

## The browser: a redraw, and the heap over redraws (LV-23, and PV-48's browser half)

`harness/perf-memory.spec.mjs.txt` on `/perf-live-memory?b=10|100|400`. A redraw is timed from the click to
the viewport's new text; that is the DOM, not the frame the browser paints.

| Report rows | Side | Redraws | First report, s | Redraw, ms: median (least–most) | Heap after a full collection, MiB: loaded → after redraw 5 → last | MiB a redraw over the last 10 | WebAssembly heap, MiB: loaded → last |
|---|---|---|---|---|---|---|---|
| 11,001 | `main` | 20 | 1.51 | 112 (90–195) | 13.8 → 36.1 → 102.8 | +4.45 | 67 → 239 |
| 11,001 | branch | 20 | 1.67 | 73 (63–225) | 15.3 → 19.9 → 25.0 | +0.17 | 80 → 96 |
| 11,001 | branch | 100 | 1.59 | 64 (48–225) | 15.3 → 19.9 → 20.3 | +0.11 | 80 → 115 |
| 101,001 | `main` | 20 | 2.40 | 780 (710–884) | 83.1 → 293.3 → 911.7 | +41.22 | 166 → 1,466 |
| 101,001 | branch | 20 | 3.07 | 90 (65–231) | 94.2 → 129.6 → 137.6 | +0.07 | 199 → 239 |
| 101,001 | branch | 60 | 3.21 | 74 (56–218) | 94.2 → 129.6 → 148.6 | +0.07 | 199 → 239 |
| 401,001 | `main`, out of memory on redraw 7 | 6 | 6.68 | 3,182 (3,056–3,295) | 302.8 → 1,060.4 → 1,211.9 | +151.52 | 495 → 2,008 |
| 401,001 | branch | 20 | 8.25 | 96 (75–266) | 347.6 → 401.2 → 420.0 | +0.08 | 591 → 687 |

- **`main` grows by one report a redraw, without end**: 4.5, 41.2 and 151.5 MiB at the three sizes. At
  401,001 rows the runtime failed on the 7th redraw (`Garbage collector could not allocate 16384u bytes of
  memory for major heap section`), as it did on 2026-10-06; the run was stopped there.
- **The branch rises by one report a redraw for the first few** — 0.9, 7.0 and 17.7 MiB — while the Change
  Highlight's earlier reports gather up to ADR-0153's bound, ⌈`ChangeHighlightDuration` / the time between
  redraws⌉ + 1, and then holds. The bound is set by time: this loop collects after every redraw, so a few
  redraws fit in the highlight's second, six earlier reports at 101,001 rows. The 60-redraw run took one
  more report at redraw 27 (+7.0 MiB), as its redraws quickened: a median of 90 ms before, 68 ms after.
- **After that, +0.07 to +0.17 MiB a redraw, and it does not accumulate**: each batch adds a segment of
  1,000 rows to the Snapshot, which compacts once more than 64 have piled up
  (`SnapshotTuning.MaxBatchSegments`), at the 65th batch here. The 100-redraw run at 11,001 rows rose to
  31.1 MiB at redraw 65 and was back at 17.7 MiB by redraw 74.
- **The heap the merged code levels off at is higher than the Claude Code track's**, which held one report
  (89.5 MiB flat at 101,001 rows on 2026-10-07): the user decided on 2026-10-09 that the report source keeps
  the reports the Change Highlight compares, so that a cell scrolled into view shows its mark (ADR-0153,
  "Why reports, and not times"; LV-22).

## A million trades in the browser (PV-21, beside PV-48)

`measure-pivot.spec.mjs` over a million trades (`raw/pv21/`): the gestures on `/pivot`, questions up to the
200,000-leaf cap and past it, 1,000 changes on `/pivot-live`, and a CSV of a million rows on `/pivot-csv`.
Times in the page's own clock, from the input to the frame after the answer; medians, milliseconds. PV-21's
targets are the user's "snappy" (Q52).

| Gesture or question | PV-21's target | `main` | branch |
|---|---|---|---|
| First visual answer, every gesture | ≤ 100 | 12–48 | 13–53 |
| Sort, collapse, expand, change of form | ≤ 100 | 17–26 | 18–29 |
| A new question from the page: tick or untick Book, filter to USD or (All) | ≤ 300 | 110–153 | 141–179 |
| A question of 1,350 combinations | ≤ 300 | 187 | 258 |
| 13,500 combinations | ≤ 300 | 298 | **371** |
| 27,000 combinations | ≤ 300 | 465 | 697 |
| 66,150 / 134,730 / 198,450 combinations (near the cap) | ≤ 300 | 802 / 1,917 / 1,850 | 1,092 / 2,438 / 2,958 |
| 330,750 combinations, refused past the cap | ≤ 300 | 288 | 380 |
| The first question at load | | 385 | 454 |
| 1,000 changes on `/pivot-live`, Apply to the report's frame | ≤ 200 | 32 (25–48) | 37 (32–80) |
| A CSV of a million rows: read / first question / report | ≤ 4,000 | 3,312 / 332 / 3,644 | 3,392 / 412 / 3,804 |
| The page blocked at once: the longest task, CSV / near the cap | ≤ 50 | 122 / 93–193 | 174 / 124–145 |
| Arrow from the demo API, read again / objects into a Snapshot | | 2,620 / 2,563 | 2,619 / 2,658 |

- **Every new question is slower on the branch**, by 16% (tick Book) to 60% (198,450 combinations), on every
  question asked; reading the data is within 4% (Arrow, objects, the CSV's read). Where the time goes is
  the next section. The branch misses one target `main`
  met: the 13,500-combination question, 371 ms against PV-21's 300 (`main` 298). Neither side meets 0.3 s
  from 27,000 combinations up, or 50 ms of blocking.
- **The live update over a million trades is about even.** Its report is small (14 rows), so the branch's
  gain shows only as the report grows: the tables above.

## Where a new question's extra time goes

Measured after PV-21, the same day. The five questions were timed on CoreCLR through the component
(`harness/PerfQuestion.cs.txt`: bUnit, `DOTNET_TieredCompilation=0`, the demo's million trades, slicing off,
each question asked after the page's first layout, five runs after one, two rounds a side). Three of them
were profiled in the browser (`harness/profile-question.cjs.txt`): Chrome's CPU profiler from the Update
click to the answer, three runs each, every sample named through the runtime pack's symbol map for
`dotnet.native.wasm`, which the published hosts carry unchanged (`raw/question/`). The profiler slows
both sides; the split is what it says.

| Combinations | CoreCLR, ms: `main` / branch | Allocated a question, MiB: `main` → branch | Browser, profiled, ms: `main` / branch | of which .NET code | of which the collector | idle |
|---|---|---|---|---|---|---|
| 1,350 | 16 / 19–20 | 9 → 14 | 235–370 / 282–299 | 200–227 / 234–262 | 3–101 / 2–4 | 21–26 / 15–27 |
| 13,500 | 25–26 / 37 | 18 → 26 | | | | |
| 66,150 | 83–147 / 113–116 | 67 → 88 | | | | |
| 134,730 | 459–495 / 283–302 | 171 → 205 | 2,047–2,117 / 2,910–3,116 | 1,697–1,797 / 2,377–2,459 | 239–273 / 400–540 | 11–23 / 7–20 |
| 198,450 | 318–319 / 381–404 | 137 → 211 | 2,144–2,153 / 3,164–3,213 | 1,851–1,854 / 2,794–2,834 | 207–223 / 269–272 | 10–23 / 14 |

The browser's columns leave out each layout's first run, which compiles what it runs.

- **Nothing waits.** The main thread is idle for 7–27 ms of a question on either side, so slicing's yields
  cost nothing measurable.
- **Most of the extra is the branch's .NET code, run by the browser's interpreter**: 0.7 s of 0.9 s at
  134,730 combinations, 1.0 s of 1.04 s at 198,450. The collector takes the rest, 0.2 s at 134,730
  combinations, where the branch allocates 205 MiB a question against 171 and keeps more of it.
- **On CoreCLR the same questions are not uniformly slower.** At 134,730 combinations the branch is 40%
  faster; it is slower for the smallest questions and at 198,450 combinations, by 20 to 45%. Taking PV-21's
  own times, the browser multiplies `main`'s CoreCLR time by 4 to 6 for the large questions and the
  branch's by 7.5 to 8.5. In the branch's profile the hottest native frames are the interpreter itself,
  `memmove` and `memset` (arrays copied and cleared) and `get_virtual_method_fast` (virtual calls): work
  a JIT inlines or devirtualises, and the interpreter does as written. `main`'s raw profile was not kept
  to compare frame by frame.
- **What the branch builds that `main` does not** is the state a live update folds into. That is the pass
  that keeps each row's leaf, the incremental cube, and the report's parts that versions share, all built
  with the report. A live update over that state costs 4–22 ms at 401,001 report rows, against `main`'s 600.
- **Which of those costs most in the browser is not separated.** The interpreter's frames name no .NET
  method, and the jiterpreter's traces are anonymous modules. A build timed phase by phase in the browser
  would say.

## Not measured

- **The frame the browser paints, for the report sizes of PV-48.** The loop times the click to the changed
  DOM; PV-21 times its frames, over a small report.
- **The Server host.** Everything above is CoreCLR in-process or WebAssembly.

## Reproducing

From the repository root, a worktree for each side, with the harness files copied to where they build
(`git add` them so nix sees them; never commit them there):

| File | Where it builds |
|---|---|
| `PerfWholeUpdate.cs.txt` | `tests/ExPivot.Components/PerfWholeUpdate.cs`, its `@@…@@` marks filled as above |
| `ReportSteps.cs.txt` | `tests/ExPivot.Engine.Tests/ReportSteps.cs` |
| `ReportRendering.cs.txt` | `tests/ExPivot.Components/ReportRendering.cs` |
| `PerfGrid.*.txt` | `spikes/live-update/PerfGrid/` (`Program.cs`, `CountingRenderer.cs`, `Support.cs`, `Trades.cs`, `PerfGrid.csproj`) |
| `PerfLiveMemoryPage.razor.txt` | `samples/ExGrid.DemoPages/Pages/PerfLiveMemoryPage.razor` |
| `perf-memory.spec.mjs.txt`, `perf-memory.config.mjs.txt` | `tests/ExGrid.Browser/` (copied in by the run, removed after) |
| `PerfQuestion.cs.txt` | `tests/ExPivot.Components/PerfQuestion.cs`, beside `samples/ExGrid.DemoPages/DemoPivotData.cs` copied in as `MeasurementDemoPivotData.cs`; on `main` the report token is `ExPivot.Report` and its row count `Report.Rows.Count` |
| `profile-question.cjs.txt` | run with node against a served host: `node profile-question.cjs <url> <out.json> <dotnet.native.js.symbols of the runtime pack>` |

Build each side in Release (`nix develop -c dotnet build tests/ExPivot.Components -c Release`, the same for
`tests/ExPivot.Engine.Tests`, `spikes/live-update/PerfGrid` and `samples/ExGrid.DemoApi`), publish its DemoHost
(`nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o <hosts>/wasm`), write the CSV with
`harness/csvgen.*.txt` (a console project referencing `samples/ExGrid.DemoPages`), and run
`harness/run-final.sh.txt`, then `run-sizes.sh.txt`, `run-long.sh.txt` and `run-pv21.sh.txt`: they name the
scratch paths of this run, which a rerun replaces. `harness/pv21.py` prints the two sides' PV-21 records
side by side. One test class runs from its build folder, since `dotnet test --filter` does
not filter under Microsoft.Testing.Platform:

```sh
cd tests/ExPivot.Components/bin/Release/net10.0
DOTNET_TieredCompilation=0 PERF_OUT=<dir> PERF_ADVANCE_MS=1 PERF_INNERS=10,100,400 PERF_BATCHES=1,1000 PERF_RUNS=9 \
  nix develop <repo> -c dotnet ExPivot.Components.Tests.dll -class ExPivot.Components.Tests.PerfWholeUpdate
```

Do not run these beside another measurement, a build or a layer-3 run, and do not rebuild a host while it
is served.
