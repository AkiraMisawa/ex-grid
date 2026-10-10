# Live updates: what one update costs, step by step (ticket 01, Claude Code's run)

Date: 2026-10-06. Branch `claude/live-data-next-cc`, based on `41c8d8c8` (origin/main). No file under
`src/` was changed; every step was reached from outside, by reflection where it is private. The
harness is recreated, not committed: a copy is in [`harness/`](harness/README.md).

This is ticket 01 of [the live-data spec](../../docs/specs/live-data/issues/01-measure-what-an-update-costs-end-to-end.md),
in the shape of [`2026-10-05-macos-live-update-measure`](../2026-10-05-macos-live-update-measure/README.md).
The user ran a second track on the same ticket at the same time, for comparison. This directory
carries the `-cc` suffix so the two do not collide, and neither read the other's results. It decides
nothing: no ADR, no `CONTEXT.md` entry and no Definition of Done criterion is changed. Where the
numbers call for a decision, it is written as a proposal in the last sections.

Performance never gates (AGENTS.md). Every number here is observational.

*(Added 2026-10-07 by the team lead: the out-of-memory's cause was found the same day — ExGrid's kept
paints held row instances, and a report row holds its whole report and cube —
[`2026-10-06-macos-pivot-oom`](../2026-10-06-macos-pivot-oom/README.md). What was decided on these numbers
is [ADR-0160](../../docs/adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md),
[ADR-0161](../../docs/adr/0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md), ADR-0141's
note of 2026-10-07 and the rewrite of
[ADR-0142](../../docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md).
Where this record says the cause is under investigation, that is the state it was written in.)*

## The results in brief

Milliseconds. CoreCLR is the least of 15 runs; the browser is WebAssembly (published, no AOT,
headless Chrome), from Apply to the painted frame that shows the change, least / median. ExPivot at
about 4×10⁵ report rows is taken from CoreCLR: in the browser only one redraw at that size was
timed cleanly before the runs ran out of memory.

| Product | Size | Changes | Whole update, CoreCLR | Where its time goes (CoreCLR) | Browser, Apply → painted |
|---|---|---|---|---|---|
| ExGrid, keyed `GridSource.From` | 10⁵ rows | 1 | 0.100 (median 0.279) | the requery at the least; the Selection Summary's walk on the median | 3.2 / 16.0 |
| | | 100 | 0.290 | **publication** (66%): requery, Change Highlight | 6.4 / 10.1 |
| | | 1,000 | 2.02 | **publication** (60%; the Change Highlight's record 31%, the requery 18%), fold 28% | 18.0 / 25.2 |
| | 10⁶ rows | 1 | 0.745 (median 3.07, most 10.1) | **the Selection Summary's walk of the new Window** on the median (up to 8.6–11 ms); the requery at the least | 5.0 / 91.4 (most 135) |
| | | 100 | 0.922 | **publication** (66%; the requery 51%) | 7.9 / 13.3 |
| | | 1,000 | 3.34 | **publication** (67%; requery 33%, record 22%), fold 30% | **22.7 / 28.6** (LV-15) |
| ExPivot, live redraw | 10,001 report rows | 1 | 3.53 | **ReportAsync** 37%, CubeAsync 30% | 57 / 78 |
| | 101,001 | 1 | 60.2 | **ReportAsync** 53%, CubeAsync 31% | 529 / 563 |
| | 401,001 | 1 | 268.5 | **ReportAsync** 59%, CubeAsync 30% | 2,338 (one redraw); longer runs ended **out of memory** (below) |

Three findings beyond the steps the ticket named:

- **ExPivot ran out of memory in the browser under live data** — an observed finding whose cause is
  under investigation (the team lead's, separately). At 401,001 report rows, runs of several redraws
  ended in `System.OutOfMemoryException` at `PartColumns..ctor` in `PivotCube.BuildAsync`, while a
  diagnostic run of one redraw worked (2,338 ms). At 201,001 rows one run went dead after 21.8 s of
  the renderer's CPU. On CoreCLR, measured separately, every report and cube a live ExPivot showed
  stayed alive across 20 redraws, and the heap grew by 54 MB a redraw at 101,001 rows. See
  "ExPivot out of memory in the browser".
- **The grid walks every new Window for the Selection Summary** (`NoteRowsForSummary`,
  `ExGrid.Summary.cs:81`), whether or not its source vouches: from the top to the first position
  whose row changed. That is nothing when the busy rows are at the top, and the whole Window when the
  only change is at the bottom: 8.6–11 ms at 10⁶ rows on CoreCLR, and in the browser about 14 ms per
  100,000 rows walked (about 140 ms at 10⁶). It is the largest cost of a one-row update on a large
  Window, and LV-10's vouch does not remove it.
- **A pushed Window's check is most of an update's cost for a large ExGrid page** (ticket 02):
  checking 10⁶ string Row Keys takes 40 ms on CoreCLR and 257 ms in the browser, against 3.3 and
  22.7 ms for the whole keyed update of 1,000 changes. For ExPivot's report the same check is about 3%
  of a redraw.

## Environment

- **Machine.** Apple M4 Pro, 12 cores (8 performance, 4 efficiency), 24 GB, macOS 26.6.2 (25G83).
- **.NET.** SDK 10.0.203 through `nix develop`, runtime 10.0.7, CoreCLR, workstation GC, Release,
  `DOTNET_TieredCompilation=0` for every CoreCLR run (the ticket's method: with tiering on, the first
  configuration measured slower than the later ones).
- **Browser.** Google Chrome 154.0.8037.98 through Playwright (`--project=chrome`), headless
  (`EXGRID_HEADLESS=1`, as this Mac's layer-3 runs are), viewport 1400 × 1100. Headless Chrome
  renders in software and runs a frame's animation callbacks as soon as the task before them ends:
  the browser numbers are read against each other and against CoreCLR's, never as what a person on
  real hardware sees.
- **Builds.** The WebAssembly DemoHost, the Server host and the demo API server published with
  `dotnet publish -c Release` (no AOT) and served as CI's full run serves them (`EXGRID_HOSTS`), on
  ports 5599 (WebAssembly) and 5598/6598/7598/8598 (Server). Each port was checked free first; only
  processes this run started were stopped, by PID.
- **Background load.** Not a quiet machine, and recorded beside every batch (`raw/contention.log`;
  each CoreCLR JSON carries a load average and the busiest processes before and after):
  - an Android emulator, a virtual machine and Docker throughout (5–17% of a core each);
  - **the five `ExGrid.DemoApi` processes named in the brief (ports 8591–8594 and 8611) were stopped
    at about 13:10 BST**, with the user's OK. Every timed batch here started at 13:40:52 or later, so
    none ran with that load; only untimed smoke runs of the harness came before (noted in
    `raw/contention.log`);
  - from about 13:47 an animated wallpaper extension (`Ventura.appex`, with WindowServer at 30–50%
    of a core), and from about 14:30 macOS's idle-time work: `mediaanalysisd` (up to 160%),
    `knowledgeconstructiond`, FileProvider checks, Spotlight. Load average 1.4–2.5 for the first
    CoreCLR runs, 3–5 for the rest, briefly 7–8.
  - The first runs of ExPivot at k = 1,000, at 101,001 and 401,001 rows, measured the cube and
    report steps twice as slow as k = 1 with a tight spread, while their whole redraw did not move:
    a scheduling effect (the wallpaper extension started then). They were run again with k = 1,000
    first and k = 1 after it, and gave the k = 1 numbers (17.6 and 30.9 ms; 81.4 and 159.1 ms). The
    reruns stand in the tables; the first runs are kept in `raw/`.
- **The other track** — see "Contention log" at the end.

## Method

- **Each step alone, on the same inputs, the least of 15 runs** (after 3 untimed), a full collection
  before each, as the ticket asks. The median and the most are kept beside the least in every raw
  file, so a wide spread shows.
- **And the whole update as the product runs it**, so the steps can be checked against it. The
  steps' least values add up to the whole update's least within 0.13 ms for ExGrid, and within 7% for
  ExPivot's redraw.
- **CoreCLR** (`harness/spikes/live-update/Costs`): ExGrid and ExPivot rendered by M2's counting
  renderer (no DOM; JavaScript answered with defaults). It also times the renderer's
  `ProcessPendingRender`, counts the row components that render and mount, and estimates each
  render batch's bytes from `RenderBatchWriter`'s layout, as M2 did. ExGrid runs
  `/grid-live-local`'s trades, columns, Viewport (480 × 1270, 18 painted rows) and amendment (half of
  each batch on the first 500 trades). ExPivot runs D10's report: two text fields, A00000… × B0000…,
  1,000 A Items with their subtotals, 9, 100 or 400 B Items, one record per leaf, a sum of one value,
  Viewport 300 × 900 (11 painted rows), changes picked uniformly; slicing off, so a redraw runs
  inline and its steps cost what they cost.
- **The browser** (`harness/tests/ExGrid.Browser/measure-live-costs.spec.mjs`): measure-live's method
  (the page's own clock; a MutationObserver installed before the app's first script), with M1's
  paint stamp added: the first task after the frame that shows the change, once its style, layout and
  paint are done. `/grid-live-local` as it is, with `?probe=1` for a run that also times a second
  source bound to nothing on the same batch, and the grid's two checks over the Window in hand.
  ExPivot on a harness page, `/pivot-live-costs`, D10's report with default slicing: one batch per
  press of a step button; the batch always amends the first record too, and the redraw has landed
  when the report shows that record's new value.
- **Server bytes**: M2's method on the published Server host, one batch per press with the page itself
  never rendering after it: the render batch as Chrome reports it (decompressed), and the bytes
  Kestrel wrote to the circuit's connection, read from a connection counter added to the host for the
  run (`GET /api/wire`, copied from `spikes/render-bench/Bench.Server`). A second render batch
  follows each update a second later, when the Change Highlight's marks end; it is recorded too.

## ExGrid: `GridSource.From` with a Row Key (`/grid-live-local`)

### CoreCLR, step by step

Milliseconds, the least of 15 runs (`raw/grid-1e5.json`, `raw/grid-1e6.json`).

| Step | 10⁵, k = 1 | 10⁵, k = 100 | 10⁵, k = 1,000 | 10⁶, k = 1 | 10⁶, k = 100 | 10⁶, k = 1,000 |
|---|---|---|---|---|---|---|
| Fold: the source takes the batch in (`Apply`, gathering) | 0.008 | 0.077 | 0.570 | 0.028 | 0.156 | 1.015 |
| Publication (`PublishGathered`) | 0.051 | 0.191 | 1.218 | 0.390 | 0.606 | 2.230 |
| &nbsp;&nbsp;of which the incremental requery (`LiveRequery.Apply`, alone) | 0.045 | 0.075 | 0.356 | 0.372 | 0.469 | 1.105 |
| &nbsp;&nbsp;of which the Change Highlight's record (`CellChangeTimes.Record`, alone) | 0.007 | 0.071 | 0.631 | 0.026 | 0.103 | 0.720 |
| The grid takes the Window in (`ApplyState`) | 0.014 (median 0.332, most 0.696) | 0.007 | 0.010 | 0.103 (median 2.33, most 7.56) | 0.017 | 0.017 |
| &nbsp;&nbsp;of which the Selection Summary's walk (`NoteRowsForSummary`, alone, on the last update) | 0.334 | 0.006 | 0.007 | 5.99 | 0.014 | 0.016 |
| &nbsp;&nbsp;where that walk stopped (the first changed position) | 68,313 | 1 | 0 | 683,139 | 13 | 0 |
| &nbsp;&nbsp;the walk when only the last row changed (alone) | 0.488 | 0.484 | 0.520 | 8.62 | 8.92 | 11.1 |
| Render (`StateHasChanged`; the renderer's part in brackets) | 0.062 (0.047) | 0.079 (0.061) | 0.118 (0.093) | 0.103 (0.072) | 0.125 (0.087) | 0.154 (0.119) |
| **Whole update** (`Apply` on the renderer's context, interval 0) | 0.100 (median 0.279, most 0.663) | 0.290 | 2.016 | 0.745 (median 3.07, most 10.1) | 0.922 | 3.344 |
| Painted rows rendered per update, of 18 (median, range); none mounted | 0 (0–0) | 1 (0–5) | 9 (4–12) | 0 (0–0) | 3 (0–5) | 11 (5–14) |
| Render batch per update, estimated bytes (M2's transcription, median) | 56 | 464 | 1,471 | 56 | 586 | 1,718 |

- **The dominant step.** At 100 and 1,000 changes it is the source's publication. Within it, the
  Change Highlight's record (each changed row's eleven painted texts formatted twice and compared)
  costs more than the requery at 10⁵ rows and two thirds of it at 10⁶. At one change it is the requery
  when the change is near the top, and the grid's walk of the new Window when it is not.
- **The grid's own pass over a vouched Window is gone** (LV-10): `ApplyState` costs 0.007–0.017 ms at
  either size, apart from the Selection Summary's walk. Confirmed by count (`raw/vouch.json`): over a
  new Window of 100,000 rows, the source's own key was called 0 times in `ApplyState`; a Row Key of
  the grid's own over the same source, 100,000 times.
- **The render** is 0.06–0.15 ms. Only rows with a new instance render (ADR-0003), none is mounted
  (ADR-0140), and the grid itself renders once even when no painted row changed (an empty diff,
  56 bytes).

### The browser (WebAssembly)

Milliseconds, least / median / most of 15 runs, `raw/browser-results.json`.
"Applied" is the page's own timing of `Apply`, which on WebAssembly includes the publication, the
grid's taking in and its render with the DOM's update, all in that task.

| | 10⁵, k = 1 | 10⁵, k = 100 | 10⁵, k = 1,000 | 10⁶, k = 1 | 10⁶, k = 100 | 10⁶, k = 1,000 |
|---|---|---|---|---|---|---|
| Applied | 2.6 / 15.5 / 24.5 | 5.5 / 9.0 / 23.4 | 16.4 / 24.0 / 31.4 | 4.5 / 91.1 / 135 | 6.8 / 11.5 / 78.3 | 21.7 / 27.6 / 86.3 |
| **Apply → painted frame** | 3.2 / 16.0 / 25.2 | 6.4 / 10.1 / 24.3 | 18.0 / 25.2 / 32.6 | 5.0 / 91.4 / 136 | 7.9 / 13.3 / 79.3 | **22.7 / 28.6 / 87.3** |
| The frame's style, layout and paint | 0.3–0.6 | 0.4–1.7 | 0.9–2.0 | 0.2–0.5 | 0.6–2.5 | 0.8–1.4 |
| Runs where the grid's DOM changed | 0 of 15 | 12 of 15 | 15 of 15 | 0 of 15 | 13 of 15 | 15 of 15 |
| The source alone, the same batch (`?probe=1`) | 0.3 / 0.5 | 1.8 / 3.2 | 12.8 / 16.0 | 1.3 / 1.5 | 2.7 / 2.9 | 14.9 / 15.4 |
| A pushed Window's check by key (`RequireDistinctKeys`, alone) | 25.1 / 32.5 | 26.2 / 30.0 | 23.5 / 26.1 | 257 / 261 | 257 / 265 | 269 / 278 |
| … by instance (`RequireDistinctRows`, alone) | 13.1 / 14.5 | 12.6 / 14.3 | 12.6 / 13.2 | 130 / 132 | 129 / 133 | 139 / 163 |

- **LV-15**: 1,000 changes to 10⁶ rows, Apply to the painted frame, 22.7 ms least and 28.6 ms median
  (87 ms at most, once), against PV-21's 0.2 s.
- **The source is most of an update at 1,000 changes** (about 15 of 22 ms at 10⁶); the grid's taking
  in, its render and the DOM are the rest, 3–7 ms.
- **The walk, read off the positions.** The probe runs at 10⁶ also logged the first changed position
  of each batch. At one change, `Applied` against it fits a line of 14.4 ms per 100,000 rows walked
  (r = 0.95, 15 batches): a one-row update to the last rows of a million costs 120–135 ms in the
  browser, nearly all of it this walk, and one to the first rows 3–5 ms. (Two runs taken for this at
  16:12–16:21 gave 13.4 and 12.2 ms per 100,000 rows; they ran when the team lead's investigation
  may have been using the machine, and are not used.)
- **WebAssembly against CoreCLR** at 10⁶: the source 4.6 times (14.9 against 3.2 ms), the key check
  6.4 times, the instance check 5.4 times, the walk about 15 times.
- The probe's own long task (about 400 ms at 10⁶) delays the next paint, so the probe runs give only
  their probe numbers; the plain runs give the timings above.

### The bytes per update on the Server host

Median (range) of 15 presses at each size (`raw/server-bytes.json`, derived from the per-connection
counts in `raw/browser-results.json`); the WebSocket's per-message compression on
(`permessage-deflate; client_max_window_bits=15`). The same at 10⁵ and 10⁶ rows to within a few
bytes, as it should be: only painted rows travel.

| Changes | The update's render batch | On the wire | The un-mark a second later: batch / wire | Per update in all, wire (mean) |
|---|---|---|---|---|
| 1 | 73 (73–73) | 19 (19–23) | none | 20 |
| 100 | 641–644 (73–1,161) | 71 (14–221) | 301 (0–537) / 20–24 (0–102) | 102–113 |
| 1,000 | 2,562–2,565 (1,805–3,641) | 291–297 (179–465) | 1,293 (905–1,809) / 116–133 (45–210) | 412–424 |

- **The un-mark is a second render batch per update**: when a mark's second is over, the grid renders
  the row again to take the class off. It carries about half the update's bytes.
- **The transcription in .NET under-counts the real batch** by about a third (1,718 estimated against
  2,562 measured at 1,000 changes): the CoreCLR pass ran its updates within the Change Highlight's
  second, so cells already marked gained no class, while the presses here come 1.4 s apart.
- The browser's own messages per press were 395 bytes (the click's event and `OnRenderCompleted`).

## ExPivot: a live redraw

### CoreCLR, step by step

Milliseconds, the least of 15 runs. k = 1,000 at 101,001 and 401,001 rows is the rerun (see
"Background load"). `raw/pivot-1000x9.json`, `pivot-1000x100*.json`, `pivot-1000x400*.json`.

| Step | 10,001 rows, k = 1 | 10,001, k = 1,000 | 101,001, k = 1 | 101,001, k = 1,000 | 401,001, k = 1 | 401,001, k = 1,000 |
|---|---|---|---|---|---|---|
| The source folds the batch (`SnapshotPivotSource.Apply`) | 0.009 | 0.629 | 0.071 | 0.864 | 0.230 | 1.34 |
| The source answers from the pass it holds (`AggregateAsync`) | 0.207 | 0.258 | 2.25 | 2.34 | 9.59 | 9.64 |
| `PivotEngine.CubeAsync` | 1.06 | 1.10 | 18.4 | 17.6 | 80.1 | 81.4 |
| `PivotEngine.ReportAsync` (with the keys it makes) | 1.30 | 1.36 | 31.6 | 30.9 | 157.2 | 159.1 |
| `HasSameRowsAsAsync` | 0.088 | 0.093 | 1.43 | 1.32 | 4.03 | 4.35 |
| `LabelWidthsAsync` | 0.321 | 0.321 | 3.30 | 3.23 | 13.0 | 12.5 |
| `Show` (the history, the columns) | 0.014 | 0.021 | 0.035 | 0.033 | 0.039 | 0.038 |
| The grid checks the report's keys (`RequireDistinctKeys`, alone) | 0.144 | 0.146 | 1.63 | 1.74 | 8.21 | 8.11 |
| &nbsp;&nbsp;beside it, by instance (`RequireDistinctRows`, alone) | 0.127 | 0.130 | 2.66 | 2.17 | 9.83 | 12.0 |
| The grid takes the report in (`ApplyState`, alone) | 0.149 | 0.158 | 1.61 | 1.73 | 8.36 | 8.42 |
| Render, the grid's `ApplyState` within it | 0.281 | 0.333 | 2.11 | 2.11 | 8.84 | 9.26 |
| **Whole redraw** (`Apply` on the renderer's context, slicing off) | 3.53 | 4.32 | 60.2 | 62.9 | 268.5 | 276.0 |
| ReportAsync / CubeAsync / the key check, share of the whole | 37% / 30% / 4.1% | 31% / 26% / 3.4% | 53% / 31% / 2.7% | 49% / 28% / 2.8% | 59% / 30% / 3.1% | 58% / 30% / 2.9% |
| Painted rows: painted, rendered, mounted (median) | 11, 11, 0 | 11, 11, 0 | 11, 11, 0 | 11, 11, 0 | 11, 11, 0 | 11, 11, 0 |
| Painted rows whose painted value changed (mean; most) | 0.00; 0 | 2.60; 4 | 0.00; 0 | 0.87; 2 | 0.00; 0 | 0.73; 1 |

- **The dominant step is laying the report out** (`ReportAsync`), 31–59% and growing with the
  report, then **making the cube** (`CubeAsync`), 26–31%. Together 57–89% of a redraw. The fold is
  under 1% at every size and at either batch size; the answer from the held pass 3–6%;
  `HasSameRowsAsAsync` and `LabelWidthsAsync` 6–7% together; `Show` nothing.
- **The grid's check of the report's keys is 2.7–4.1%** of a redraw, about as much as the check by
  instances (D10 measured 8.3 against 9.5 ms at 401,001 rows; here 8.1–8.2 against 9.8–12.0). It is
  nearly all of the grid's `ApplyState`, and most of what the render line shows: the rows' own render
  is 0.13–0.85 ms.
- **M3's count, with the Row Key in.** Every painted row renders on every redraw (11 of 11), none is
  mounted (PV-42 holds). With one change a batch, none of the 11 had a painted value change; with
  1,000 changes, 0.7–2.6 of them on average. So **8–11 of 11 row renders per redraw are of rows whose
  painted values did not change** — the renders that keeping row instances and holding one Change
  Highlight delegate would save. On CoreCLR they cost well under a millisecond in all.

### The browser (WebAssembly)

Apply to the painted frame that shows the new report, ms, least / median / most
(`raw/browser-results.json`, `raw/pivot-wasm-progress.jsonl`). Default slicing (30 ms), as a
Consumer runs it: the redraw spreads over turns, and `Applied` includes its first slice.

| Report rows | Changes | Applied | **Apply → painted report** | Longest task | Redraws before running out of memory |
|---|---|---|---|---|---|
| 10,001 | 1 | 29.1 / 36.2 / 49.0 | 57 / 78 / 81 | 51 (1 of 15 runs had one) | — |
| 10,001 | 1,000 | 37.2 / 43.1 / 55.5 | 68 / 71 / 94 | 59 | — |
| 101,001 | 1 | 31.6 / 32.1 / 33.9 | 529 / 563 / 692 | 58 median, 170 most | — (15 of 15) |
| 101,001 | 1,000 | 40.9 / 43.9 / 74.9 | 555 / 567 / 672 | 55 median, 132 most | — (15 of 15) |
| 201,001 | 1 | — | not reported (see below) | — | the 15:49 run went dead after 21.8 s of renderer CPU |
| 401,001 | 1 | 38.9 | **2,338** (one redraw, the diagnostic run) | 75 (3 long tasks, 191 in all) | the 15:29 run: out of memory after several redraws |

The steps that can be run from outside the component, timed by the page before each batch
(`?probe=1`; least / median), against CoreCLR's least:

| Report rows | `PivotEngine.Cube` | `PivotEngine.Report` | The key check | The instance check |
|---|---|---|---|---|
| 10,001 | 18.6 / 22.1 (CoreCLR 1.06) | 13.5 / 17.2 (1.30) | 1.4 / 1.7 (0.144) | 0.9 / 1.1 (0.127) |
| 101,001 | 136 / 142 (18.4) | 192 / 194 (31.6) | 14.3 / 14.5 (1.63) | 9.0 / 9.2 (2.66) |

- A redraw costs about 8.8 times its CoreCLR time in the browser at 101,001 rows, and the one redraw
  timed at 401,001 rows 8.7 times (2,338 against 268.5 ms): a live report of that size would lag
  more than 2 s behind each change, against `RedrawInterval`'s 250 ms. The slicing holds the longest
  task to 50–170 ms.
- **In the browser the key check costs more than the instance check** (14.3 against 9.0 ms at
  101,001 rows); on CoreCLR it costs less. Either way it is 2–3% of a redraw.
- At 201,001 and 401,001 rows, runs of 6 to 13 redraws were also timed at 16:33–16:37, after the team
  lead's investigation had begun; their per-redraw times are kept in `raw/pivot-wasm-progress.jsonl`
  and not reported here.

### ExPivot out of memory in the browser (an observed finding; its cause is under investigation)

Found by the browser runs: an unexpected runtime exception is a finding (AGENTS.md). The team lead is
investigating it in a separate worktree; nothing here concludes its cause.

- **What the browser showed.**
  - **401,001 report rows.** The 15:29 run (k = 1) showed its first report, then on a later redraw
    the renderer logged an unhandled `System.OutOfMemoryException` at `PartColumns..ctor` in
    `PivotCube.BuildAsync` (CON-1; `raw/browser-pivot-wasm-1000x400-1-out-of-memory.log`, and the
    error in `raw/console.json`); the redraw never landed. A diagnostic run of one redraw (15:47)
    worked: 2,338 ms from Apply to the painted report. Later runs at 16:34–16:37 (k = 1, k = 1,000,
    and with the probe) ended the same way after 6 redraws (5 with the probe), as
    `raw/pivot-wasm-progress.jsonl` records, but they ran after the team lead's investigation began.
  - **201,001 report rows.** The 15:49 run went dead: Chrome's renderer used 21.8 s of CPU and then
    sat idle for over 8 minutes, and the team lead stopped it at 15:58. Its log has no exception
    text (it was stopped before the spec's timeout reported). A later run at 16:33 ended in the same
    `OutOfMemoryException` after 13 redraws.
  - **101,001 rows and below**: 15 of 15 redraws landed in every run.
- **What CoreCLR showed, measured separately** (memory only, not timing:
  `raw/pivot-memory-1000x200.json`, `raw/pivot-memory-1000x100-paths.json`). ExPivot driven as the
  browser page drives it, a full collection after each redraw:
  - every report and cube shown so far was still alive — 20 of 20 after 20 redraws — while the Change
    Highlight's history held 4 versions;
  - the managed heap grew from 154 to 1,225 MB in 20 redraws at 101,001 rows, and from 306 to
    2,358 MB at 201,001 (54 and 103 MB a redraw on average, in steps of about 95 and 190 MB with a
    smaller fall every few redraws);
  - the report grid's list of kept paints (`_paints` in `ExGrid.SeenText.cs`, at most
    `PaintsKept = 64`, kept for ADR-0142) held 21 entries. Each entry holds the row instances it
    painted, and a `PivotReportRow` holds its report (`PivotReport.cs:276`). With that list cleared by
    reflection, the heap fell from 1,225 to 313 MB, and 4 of the 20 earlier reports were left alive.
  - Whether this growth is what exhausts the browser's memory is not established here.
- ExGrid's own `/grid-live-local` ran 15 of 15 updates at both sizes with no exception.

## LV-15's parts

| Part | Recorded |
|---|---|
| 1,000 changes to 10⁶ rows on `/grid-live-local`, from Apply to the frame, in the browser (published, no AOT), against PV-21's 0.2 s | **22.7 ms least, 28.6 median, 87.3 most** (headless Chrome); 1 change: 5.0 / 91.4 / 136, the walk; 100 changes: 7.9 / 13.3 / 79.3 |
| The grid's pass over a report's Row Keys against its pass over the instances, at 10⁴, 10⁵ and 4×10⁵ report rows | CoreCLR: 0.144 / 1.63 / 8.21 ms against 0.127 / 2.66 / 9.83. Browser, at 10⁴ and 10⁵: 1.4 / 14.3 against 0.9 / 9.0; at 4×10⁵ not taken cleanly in the browser (CoreCLR only) |
| The bytes per live update on the Server host | render batch 73 / 641 / 2,562 bytes, 19 / 71 / 291 on the wire, for 1 / 100 / 1,000 changes; and a second batch to take the marks off, 0 / 301 / 1,293 bytes (0 / 20 / 116 on the wire) |
| The requery and the grid's pass per update, at 10⁵ and 10⁶ rows | requery (CoreCLR, alone) 0.045–0.356 ms at 10⁵ and 0.372–1.105 ms at 10⁶; the source in all, browser, 0.3–12.8 and 1.3–14.9 ms. The grid's pass over a vouched Window: none (0 key calls); its `ApplyState` 0.007–0.017 ms, plus the Selection Summary's walk, up to 0.52 ms at 10⁵ and 11.1 ms at 10⁶ on CoreCLR, about 14 ms per 100,000 rows walked in the browser |

## What the numbers say about tickets 02 and 03 — a proposal, not a decision

**Ticket 02 (a pushed Window vouches for its rows).**

- **For ExPivot the check is small**: 2.7–4.1% of a redraw on CoreCLR, and 2–3% in the browser. The
  vouch there saves 8 ms of 268 at 401,001 rows (CoreCLR), or 14 of about 530 at 101,001 (browser). Worth having
  for consistency; not where a redraw's time goes.
- **For an ExGrid page that pushes a large Window it is most of every update.** A page pushing the
  same 10⁶ rows that `GridSource.From` holds would pay 40–42 ms on CoreCLR and 257–278 ms in the
  browser for the check. That is 92–98% of the update on CoreCLR, and in the browser above PV-21's
  0.2 s on its own; at 10⁵, 3.0–3.3 ms (60–97%) and 24–33 ms. A string key costs 1.6–2 times the
  instance check. **So option (i) changes outcomes for large pushed ExGrid Windows**, and barely for
  ExPivot.
- **Either way, a vouch removes only the duplicate check.** The Selection Summary's walk of every
  new Window stays, up to 11 ms (CoreCLR) and about 140 ms (browser) at 10⁶ rows when the first changed row
  is near the end. If the walk is to go too, that is a decision of its own (ADR-0130's), and a cheap
  route exists only where the grid is told which positions changed.

**Ticket 03 (ExPivot keeps its report across live redraws).**

- **The time is in building the cube and the report**, 57–89% of a redraw, the larger the report the
  more, and about 9 times as long in the browser (2,338 ms for the one redraw timed at 401,001 rows). The fold that ADR-0067
  made incremental is under 1%. So **decision 1's (a)** — the engine makes the next cube and report
  from the previous ones and the leaves that changed — is where a redraw's time would be saved.
- **(b) alone** — keeping the instances of rows whose painted values did not change, after a fresh
  build — saves the render of 8–11 of 11 painted rows per redraw. On CoreCLR that is under 1 ms. On
  a circuit it should save few bytes, since a row that renders under a Row Key with nothing changed
  sends no edits (M2's variant B with no changed cell sent none; ExPivot's bytes were not counted
  here). It saves no part of the build. Decision 3 (one Change Highlight delegate across data versions) is needed for (b) to
  save even those renders.
- **The browser's out-of-memory, and what CoreCLR showed of memory, bear on decision 2 ("what a row
  is").** The cause of the out-of-memory is under investigation, and 03's design should wait for it.
  What was measured: on CoreCLR, the reports a live ExPivot showed stayed alive across redraws, and
  clearing the report grid's kept paints let 16 of 20 go. If that is confirmed as the cause, a design
  in which rows are shared between reports, (a) or (c), meets the same pointer from a row to its
  report, and the fix touches `PivotReport.cs:239`'s contract or ADR-0142: the user's call.
- Ticket 02's vouch and ticket 03 meet in the grid's check, which is about 3% of a redraw: neither
  depends on the other for its value.

## Not measured, and why

- **Real hardware, headed, and Windows.** Headless Chrome only, on one Mac; layer 3's rules make a
  headed run on Windows or Linux the place for absolute frame times.
- **The browser's split of the grid's own work** (`ApplyState` against the render and the DOM) is not
  separated: only the source alone is timed beside the whole. CoreCLR gives the split.
- **ExPivot at 201,001 and 401,001 rows in the browser.** The runs ended out of memory or went dead;
  one redraw at 401,001 rows was timed cleanly (2,338 ms). The runs that did reach 6 and 13 redraws
  were taken after the team lead's investigation began and are not reported. About 4×10⁵ is taken
  from CoreCLR. No point between 101,001 and 201,001 rows (a 1000 × 150 run was not taken, by the
  team lead's instruction to take no more timed runs). 201,001 rows were not measured on CoreCLR
  except for memory.
- **The cause of the browser's out-of-memory**: under the team lead's investigation, not concluded
  here.
- **ExPivot's bytes on the Server host**, and the memory a live ExPivot holds on a Server circuit:
  not run.
- **What the remaining 1–7% of ExPivot's redraw is** (the steps add up to 93–102% of it): not
  profiled.
- **The other track's numbers**: deliberately not read.

## Contention log

The other track (the Codex session's worktrees) was checked before and after every timed batch, by
its processes and by the age of its raw result files. **The CoreCLR batches (13:40–14:31) and the
first browser runs (14:38–14:39:49) did not overlap with it.** From 14:39:49 to 15:08 it was
measuring while the browser batch continued: the wrapper proceeded after a five-minute wait instead
of waiting for it, which was this run's mistake. **All seven results from that window were discarded**
(`raw/browser-results-overlapped-discarded.json`, logs prefixed `discarded-`) and run again later
under a wrapper that waits up to an hour for the other track to be idle and abandons the run
otherwise. One later run (201,001 rows, started 15:49) stalled — its renderer used 21.8 s of CPU and
then sat idle for over 8 minutes — and the team lead stopped it at 15:58; nothing of it was
recorded. From about 16:14 the team lead's own investigation ran in another worktree, which this
run's idle check did not watch: browser timings taken after 16:14 are not reported, while the Server
byte counts (16:21–16:33) and the CoreCLR memory runs (16:38–16:41), which load does not move, are.
No timed run was taken after 16:41. The `ExGrid.DemoApi` load of the brief stopped at about 13:10,
before every timed batch. Apart from
the other track, the machine carried desktop and macOS idle-time load from about 13:47 (see
"Background load"); the one configuration it visibly disturbed (ExPivot at k = 1,000) was run again
and the rerun stands. The Server-bytes runs twice read the wire bytes off the wrong connection (a
harness bug): the first runs were discarded and run again, and the reruns kept every connection's
counts, from which the circuit's were derived (`raw/server-bytes.json`); the spec was corrected
after. Their render-batch payloads, read off the browser, are unaffected.

## Reproducing

The harness is in [`harness/`](harness/README.md), with how to put it back and run it. In short, from
the repository root with the harness applied:

```sh
# CoreCLR: the steps
DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- grid  out.json 1000000 1,100,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- pivot out.json 1000 400 1,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- vouch out.json 100000
DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- pivot-memory out.json 1000 100 1 20

# The browser, against published hosts (from tests/ExGrid.Browser)
EXGRID_MEASURE=live-costs  EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 EXGRID_HOSTS=<hosts> EXGRID_BASE_URL=http://localhost:5599 EXGRID_HEADLESS=1 EXGRID_COSTS_OUT=out.json \
  nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome
EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=100 EXGRID_COSTS_BATCH=1 …   # as above
EXGRID_MEASURE=live-bytes  EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5598 …   # as above
```

The fixture's console record for these runs (`raw/console.json`) holds the out-of-memory error above
and nothing else (it keeps one entry per test title, the last).
