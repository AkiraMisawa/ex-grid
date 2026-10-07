# Kept paints as painted text: what proposal (A) costs a paint

*(Added 2026-10-07: proposal (A) was not taken. With these numbers in hand, the user decided that a write
lands as the user entered it, so the grid keeps no paints at all
([ADR-0142](../../docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md),
rewritten; [ADR-0160](../../docs/adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)).
The record stands for what it measured: the cost of a paint, the memory, and the collector's cost of
reclaiming a generation, which bears on
[ADR-0161](../../docs/adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md).)*

Date: 2026-10-06. Worktree `live-data-paint-text`, detached at `41c8d8c8` (branch
`claude/live-data-next-cc`). The prototype is left uncommitted in the worktree; `prototype.patch` is
its change to `src/`, nothing else. The harness is recreated, not committed (`harness/`).

This measures one proposal and decides nothing: no ADR, no `CONTEXT.md` entry, no Definition of
Done criterion and no test is changed. Where the prototype differs in meaning from the base, the
difference is written down in "Semantic differences" for the user to decide. Performance never
gates (AGENTS.md); every number here is observational.

**The question.** A live ExPivot runs out of memory in the browser because ExGrid keeps its last 64
paints for ADR-0142 (`ExGrid.SeenText.cs`, `PaintsKept = 64`), each paint keeps the row instances
it painted, and a `PivotReportRow` points at its whole report and cube. **Proposal (A)**: a paint
keeps the painted text of the cells it painted instead of the rows. A row whose instance the
previous paint painted at the same position, under the same columns, geometry, metrics, slice,
`PaintedText` and `CellAppearance`, shares that paint's string array; only new instances and rows
newly scrolled into view get their text computed. The worry was that computing text at every paint
costs performance. **V0** is the base; **V1** is the prototype.

## The results in brief

| | V0 (rows kept) | V1 (painted text kept) |
|---|---|---|
| A paint that brings every painted row (42 × 19 cells) on CoreCLR: `NotePaint` alone | 0.3–0.5 µs, 544 B | **45–61 µs, 54–67 KB** |
| The same in WebAssembly (published, no AOT, in the page) | ≈ 5 µs, 344 B | **0.44–0.52 ms, 41–51 KB** |
| Its share of the step (scroll or push + render; WebAssembly includes applying the render batch) | < 1% | **13–16% of the render on CoreCLR; 4–7% of the step in WebAssembly** |
| A paint that brings 1 or 3 new rows | < 1 µs | 3–5 µs CoreCLR; 0.03–0.04 ms WebAssembly |
| A fling frame (Placeholders: pinned columns only) | < 1 µs | 1.2 µs CoreCLR; 0.01 ms WebAssembly |
| ExPivot (11 painted rows, one text column), any paint | 1.1–3.2 µs | 1.7–3.3 µs (≤ 4% of a scroll's render; 0.1% of a live redraw's render) |
| Row components rendered and mounted, every scenario | — | **identical to V0** (and nothing added after a step) |
| Browser, scripted scrolling on `/grid-live-local` and `/wide` (≈ 200 painted cells) | page step 5.46–5.57 ms task time (median, clean runs) | page step 5.65–5.66 ms: the predicted +0.1 ms, within run-to-run spread; frames and long tasks (none) the same |
| Browser, live update at 10⁶ rows × 1,000 changes, Apply → painted frame | least 17.6 / 24.0 ms, median 30.6 / 32.8 | least 22.7 / 23.0 ms, median 32.8 / 30.2 |
| ExPivot live, browser, 1,000 × 100 (101,001 rows), managed heap after a full collection | **+41 MB per redraw**, 13 of 13 reports alive after 12 redraws | **flat at 225.2 MB from redraw 4**, 4 reports alive |
| ExPivot live, browser, 1,000 × 400 (401,001 rows), 15 redraws | ran out of memory (today's run, not repeated) | **flat at 627–663 MB**, 3 reports alive; no out of memory |
| Layers 1 and 2 | green on the base | **4 failures**, all in ADR-0142's Action path (below) |

**Verdict, in one line:** the cost is real but small. A paint that brings every painted row costs
about half a millisecond more in WebAssembly for 800 cells, inside a step of 6–10 ms and a 16.7 ms
frame, and nothing measured in the browser moved beyond run-to-run spread. What (A) cannot do as briefed is keep three
behaviours of the Action path that ADR-0142 and its tests fix; that needs a decision (below).

## Environment

- **Machine.** Apple M4 Pro, 12 cores, 24 GB, macOS 26.6.2 (25G83). Not a quiet machine: load
  average 1.3–6.5 across the batches, recorded beside each one (`raw/contention.log`; each CoreCLR
  JSON carries the load and the busiest processes before and after each scenario).
- **.NET.** SDK 10.0.203 through `nix develop`, runtime 10.0.7, CoreCLR, workstation GC, Release,
  `DOTNET_TieredCompilation=0` for every CoreCLR run.
- **Browser.** Google Chrome 154.0.8037.98, headless, through Playwright 1.62.1 (Node 26.8.1),
  viewport 1400 × 1100 (1800 × 1400 for `/paint-cost`). Headless Chrome renders in software: its
  numbers are read V0 against V1, never as what a person sees on real hardware.
- **Builds.** V0 is a `git archive` of `41c8d8c8`; V1 is this worktree. The two differ only in
  `src/ExGrid/Components/ExGrid.SeenText.cs` and `ExGrid.razor` (checked with `diff -rq`), and carry
  the same harness. The WebAssembly DemoHost was published from each with `dotnet publish -c Release`
  (no AOT) and served with `tests/ExGrid.Browser/static-host.mjs` on port 5799, checked free first;
  only the hosts this run started were stopped, by PID. The published `ExGrid` assemblies were
  checked to be the two variants (V1's contains the prototype's members, V0's does not).
- **The other agents.** The Codex track and the Fluxor spike agent shared the machine. Before every
  timed run the wrapper waited for two consecutive idle checks 45 s apart (no dotnet, testhost,
  node or Chrome-driving process of theirs using CPU, no other Playwright Chrome, no other busy
  dotnet), and checked again after the run. Round 1 logged the check after each run but did not act
  on it; reading those afterwards showed five runs that another agent's work overlapped
  (`raw/contended-runs.txt`), and those batches were run again (round 2), flagged by the wrappers
  themselves. Which numbers stand is said at each table.

## The prototype (V1)

`prototype.patch`, 213 lines added and 47 removed in two files:

- **A paint keeps text.** `Paint` keeps, per painted row, a `string?[]` of the painted text of the
  painted columns (pinned first, then the scrollable slice) — exactly what `PaintedTextFor` returns
  for that cell at paint time — and null for a position with no row in hand. `TextAt` reads it.
- **Shared with the previous paint.** In `NotePaint`, a row whose instance the previous paint
  painted at the same absolute position, under the same `Columns`, `ColumnGeometry`, slice,
  `CellTextMetrics`, `PaintedText` and `CellAppearance` (the same conditions V0 uses to decide "the
  same paint"), reuses that paint's array. Otherwise its text is computed.
- **Only the newest paint holds rows.** The newest paint keeps its row instances, geometry and the
  two lookups, for deciding whether the next render is the same paint, for reuse, and for
  `RendersRowOf`. When a newer paint is noted, they are dropped from the older one. No paint older
  than the newest holds a row.
- **An instance serial per position.** Each position carries an `int` that stays the same from one
  paint to the next while the same instance stays there. Equal serials at a position in two paints
  mean the same instance was painted there throughout. It is how an older paint's press finds its
  instance in the newest paint without holding it.
- **The Action path, for an older paint.** It holds no rows or keys, so a press judged against it
  is paired by position, and only under the order it was painted in. Under one Row Sequence
  Version a position names the same row (ADR-0011; ADR-0141: "it bumps the Row Sequence Version
  when a position names another trade"). The position comes from the browser's told row
  (`ActionPressTakenAt`'s `row`), from where the pressed instance stood before D5's take-in, or
  from Space's Focus. Under another order it answers `RenderNoLongerKept`. Against the newest
  paint, the base's code runs unchanged.
- Nothing per cell reaches JavaScript: `ex-grid.js` and the shipped assets are untouched (`git diff
  --stat -- src/ExGrid/Assets` is empty), and the markup a render writes is the same.

## Semantic differences — a decision the prototype does not make

Layers 1 and 2 with the prototype (`nix develop -c dotnet test ExGrid.slnx`, `raw/tests-v1.log`):
every suite passes except `ExGrid.Components`, **Failed: 4, Passed: 1638, Skipped: 1, Total 1643**.
On the base the same suite passes (`raw/tests-v0-write-refusal.log`, run with a filter the runner
did not apply, so it is the whole suite: Failed 0, Passed 1642, Skipped 1). The four, verbatim:

```
WriteRefusalTests.With_a_row_key_a_press_whose_row_moved_is_paired_by_key_and_fires (41ms): Assert.Empty() Failure: Collection was not empty
  Collection: [GridActionRefusal { Action = GridActionEventArgs { Row = ExGrid.Components.Tests.Support.TestRow, ColumnName = Do, ActionName = approve }, Reason = RenderNoLongerKept }]
WriteRefusalTests.With_a_row_key_a_press_whose_row_moved_and_changed_is_refused_as_changed (45ms): Assert.Equal() Failure: Values differ
  Expected: RowChanged
  Actual:   RenderNoLongerKept
WriteRefusalTests.A_told_press_whose_row_component_is_gone_is_refused_without_its_click (9ms): Assert.Same() Failure: Values are not the same instance
  Expected: TestRow { Active = True, Amount = 0, AsOf = 2026-01-01T00:00:00.0000000, Book = "Row 000000" }
  Actual:   TestRow { Active = True, Amount = 777, AsOf = 2026-01-01T00:00:00.0000000, Book = "Row 000000" }
WriteRefusalTests.An_action_press_heard_by_the_row_as_painted_is_refused_all_the_same (21ms): Assert.Equal() Failure: Values differ
  Expected: RowChanged
  Actual:   RenderNoLongerKept
```

None is a prototype bug that a fix inside (A) would remove. Each one reads something from an
**older** paint's rows that (A), as briefed, no longer has:

1. **Pairing by Row Key across an order change** (`PaintedPositionOf(paint, rowKey, key)`). This is
   settled in ADR-0142: "With a Row Key, an Action press is paired with its row by key". V1 cannot
   find the key in an older paint, so a press whose row moved under a new order is refused as
   `RenderNoLongerKept` instead of being judged on its cells (the first two tests).
   - Keeping the keys instead would not serve ExPivot. A `PivotRowKey` holds an `AxisNode`, which
     has `Parent` and `Children`, so one key pins its cube's whole row-axis tree (about 400k nodes
     at 1,000 × 400).
2. **Identifying the pressed instance in that paint without a key** (`Array.FindIndex(paint.Rows,
   ReferenceEquals pressed)`). When the pressed instance is no longer in hand — the older button's
   handler, or D5 having replaced it — and no told position comes with the press, V1 cannot place
   it and refuses as `RenderNoLongerKept` instead of `RowChanged` (the fourth test).
   - In the browser a press by mouse comes with its told row, so this case is narrower there than
     in the test.
3. **The row a refusal carries, for a told press whose row component is gone.** V0 raises the
   refusal with the instance the older paint painted, the one the user pressed. V1 does not have
   it, and raises the row the Window holds at that position now: the same row by key, or the
   instance that replaced it (the third test).
   - When no row is in hand at that position at all, V1 has no row to raise, and the prototype
     drops the press. ADR-0142's "No press is lost to Blazor" forbids exactly that.

Smaller differences, no test touched:

- A row scrolled out and back between the press and the click gets a new serial, so V1 believes
  its instance changed and answers the press itself. V0 would wait for a click that Blazor drops,
  because the component was rebuilt.
- V1 records text at paint time. V0 recomputes it at judgement time with the lookups kept from
  the paint. For a `PaintedText` delegate whose output depends on state outside the row, V1 holds
  what was painted and V0 does not. Here V1 is the more faithful of the two.

**Options, for the user** (not chosen here):

- keep the row instances, or their keys, in older paints only when a column has Actions (ExPivot
  has none, so its memory result would stand);
- a `WeakReference` per position for identity, which is exact and does not depend on timing,
  because the pressed instance is alive. It costs a GC handle and a finalizer per new row, and was
  not measured here;
- accept the differences (an ADR-0142 change).

The team lead was told of this when it was found.

## Method

- **CoreCLR** (`harness/harness.patch`: `spikes/paint-text/PaintCosts`). M2's counting renderer
  (no DOM; JavaScript answered with defaults; ticket 01's copy), ExGrid driven as the browser
  drives it:
  - a scroll offset applied through `ApplyScrollOffset`, which is what `OnScrollAsync` does with
    the offset it reads;
  - a fling settled through `OnSettled`, which is what the settle timer runs. The grid's clock is a
    `FakeTimeProvider`, so the timer never fires by itself;
  - a pushed Window set as a parameter.

  Each scenario goes from a state A to a state B. Two things are timed, each the least of 15 runs
  after 3 untimed ones, with a full collection before each run, and median and most kept beside
  the least:
  - **the step**: B's scroll or parameters and the render they cause, read from the renderer's
    `ProcessPendingRender`, with the bytes allocated and the row components rendered and mounted;
  - **`NotePaint` alone**: on state B, with the kept paints put back as they were before the step
    for every run (the list and the newest paint's settable state, by reflection), so that each
    run notes the same paint against the same previous one. Bytes come from
    `GC.GetAllocatedBytesForCurrentThread`.
  - **The grid**: a pushed Window of 20,000 typed rows under a Row Key, render-bench-like: an id,
    four texts, three dates, a number column 40 px wide whose values paint `####`, and 31 numbers,
    half formatted `#,##0.00`. 40 columns, one pinned, row height 24, Viewport 990 × 1650. That
    paints **42 rows × 19 columns** (1 pinned + a slice of 18), and a page is 42 rows. Scrolled
    to row 1,000.
  - **ExPivot**: ticket 01's D10 report at 1,000 × 100 (101,001 report rows), Viewport 300 × 900,
    11 painted rows, 2 columns. Only the value column paints text; the label column paints none.
    Its live redraw runs inline (slicing off), past the redraw interval.
  - **Scenarios**: (a) one row down; (b) one page down, which is not a fling; (c) a fling frame,
    from three pages down to six, with Placeholders painting pinned columns only; (c2) the settle
    after a fling, where the full slice is painted again; (d) 1,400 px sideways, a new slice that is
    not a fling; (e) a pushed Window with 0, 3 or all 42 painted rows new instances. On ExPivot:
    (a), (b), (c), (c2) and a live redraw.
- **WebAssembly, in the page** (`/paint-cost`, a harness page). The same grid, rows, columns and
  scenarios as the CoreCLR grid, run inside the published app.
  - Each step is timed with `Stopwatch` off the event's dispatch, so the render — the components'
    and the render batch applied to the DOM — runs inline and is timed with it. Style, layout and
    paint are not included. The timer resolves 0.1 ms here.
  - `NotePaint` alone is timed in batches of 20 (20 restores and notes, less 20 restores alone) to
    get under that resolution.
  - 15 runs after 3 untimed, interleaved V0, V1, V1, V0.
- **Browser, scripted scrolling** (`harness/scripts/browser-scroll.mjs`). Pages:
  - `/grid-live-local?rows=1000000&step=1`, paused: 18 painted rows × 11 columns;
  - `/wide`, 10⁶ rows × 100 columns, both axes virtualised: 22 rows × 10 columns.

  One step per animation frame:
  - one row a frame, 120 frames;
  - one page a frame, 60 frames;
  - a fling of three pages a frame, 60 frames, with its settle;
  - on `/wide`, a new column slice every frame, 60 frames.

  Readings: the main thread's task and script time per step (CDP `Performance.getMetrics`
  deltas), the frame intervals (`requestAnimationFrame`) and long tasks (`PerformanceObserver`).
  5 runs after 1 untimed, interleaved V0, V1, V1, V0.
- **Browser, live update** (`harness/scripts/browser-live.mjs`), a standalone port of ticket 01's
  `live-costs` measurement (same probe, flow and readings).
  - Page: `/grid-live-local?rows=1000000&batch=1000&interval=0`.
  - 15 runs after 3 untimed, interleaved V0, V1, V1, V0.
- **Memory**:
  - the out-of-memory loop as given (the team lead's `scratchpad/oom/loop.mjs`, copied unchanged to
    `harness/scripts/oom-loop.mjs`), on `/pivot-live-costs` copied from
    the `live-data-oom` worktree (its `src/` change was not copied). V1 at 1,000 × 100 for 12
    redraws and 1,000 × 400 for 15; V0 at 1,000 × 100 for 12, as a same-session baseline;
  - ticket 01's CoreCLR `pivot-memory` (1,000 × 100, one change a redraw, 20 redraws), both
    variants.

## CoreCLR: the grid, 42 painted rows × 19 painted columns

Microseconds and milliseconds, the least of 15 runs. "r1 / r2" are the two rounds: V0, V1, V1, V0.
The paint's share is V1's `NotePaint` against V1's render, least against least.

| Scenario | `NotePaint` V0, µs (r1 / r2) | **`NotePaint` V1, µs** (r1 / r2) | Bytes per paint, V0 | Bytes per paint, V1 | Render V0, ms | Render V1, ms | V1's paint, share of its render | Step bytes, V0 / V1 | Rows rendered (V0 / V1, r1 / r2) |
|---|---|---|---|---|---|---|---|---|---|
| a: scroll by 1 row | 0.5 / 0.4 | **2.7 / 2.7** (median 3.3) | 544 | 2,384 | 0.051 / 0.052 | 0.052 / 0.052 | 5% / 5% | 19,984 / 21,824 | 1 / 1 / 1 / 1 |
| b: scroll by a page | 0.3 / 0.3 | **45.1 / 47.9** (median 50.8) | 544 | 53,928 | 0.302 / 0.310 | 0.351 / 0.346 | 13% / 14% | 165,824 / 219,208 | 42 / 42 / 42 / 42 |
| c: fling frame (Placeholders) | 0.4 / 0.3 | **1.2 / 1.2** (median 1.4) | 544 | 2,464 | 0.082 / 0.081 | 0.083 / 0.089 | 2% / 1% | 83,168 / 85,088 | 42 / 42 / 42 / 42 |
| c2: fling settles (full slice painted) | 0.4 / 0.4 | **49.5 / 47.1** (median 56.0) | 544 | 53,928 | 0.269 / 0.276 | 0.347 / 0.310 | 14% / 15% | 102,184 / 155,568 | 42 / 42 / 42 / 42 |
| d: horizontal scroll to a new slice | 0.4 / 0.3 | **61.2 / 60.4** (median 66.1) | 544 | 67,216 | 0.319 / 0.320 | 0.379 / 0.371 | 16% / 16% | 77,504 / 144,176 | 42 / 42 / 42 / 42 |
| e: live, 0 painted rows new | 0.3 / 0.3 | **0.2 / 0.2** (no new paint) | 0 | 0 | 0.039 / 0.038 | 0.038 / 0.039 | — | 766,520 / 766,520 | 0 / 0 / 0 / 0 |
| e: live, 3 painted rows new | 0.4 / 0.4 | **4.8 / 5.2** (median 4.9) | 544 | 4,864 | 0.057 / 0.059 | 0.058 / 0.058 | 8% / 9% | 772,200 / 776,520 | 3 / 3 / 3 / 3 |
| e: live, every painted row new | 0.4 / 0.5 | **47.5 / 48.3** (median 50.0) | 544 | 53,856 | 0.245 / 0.246 | 0.298 / 0.295 | 16% / 16% | 839,520 / 892,832 | 42 / 42 / 42 / 42 |

- **What a full paint costs.** About 60 ns and 68 bytes per painted cell on CoreCLR: the value read,
  its format, the string, and the arrays. That is the same formatting the row's render does for
  the cells it paints, done a second time. The render rises by the paint's cost and no more
  (b: +0.04–0.05 ms; d: +0.05–0.06; e-all: +0.05).
- **A row that stays costs nothing.** One row down computes one row (2.7 µs). Three new instances
  compute three (5 µs). A Window whose painted rows did not change notes no new paint, in either
  variant.
- **A fling frame costs almost nothing** (1.2 µs): Placeholders paint the pinned columns only. The
  settle after it pays the full paint once.
- **A new slice pays the full paint.** Reuse is all or nothing across a change of slice: the
  columns the two slices share (3 of 18 here) are computed again. Reusing them per column was not
  prototyped.
- **Bytes.** A full paint allocates 54–67 KB on CoreCLR, where V0 allocated 544 B. That adds a
  third to the bytes of a page step (165,824 → 219,208) and nearly doubles a sideways step's.
- **Row renders and mounts are identical** in every scenario and every run, round 1 and round 2
  (each run's counts compared, not only the medians), and no render follows a step in either
  variant: the row-render counts that ADR-0027's invariants are read from do not move.
- **Rounds.** All four grid runs (two per variant) were clean at their checks. Round 2 repeats
  round 1 within a few percent.

## CoreCLR: ExPivot, 11 painted rows

The least of 15 runs, round 2 (round 1's V1 run was overlapped at its end; round 1, in `raw/`, agrees
within a microsecond).

| Scenario | `NotePaint` V0, µs | **`NotePaint` V1, µs** | Bytes V0 | Bytes V1 | Render V0, ms | Render V1, ms | V1's paint, share of its render | Rows rendered V0 / V1 |
|---|---|---|---|---|---|---|---|---|
| a: scroll by 1 row | 1.1 | **1.7** (median 2.4) | 296 | 544 | 0.053 | 0.046 | 3.6% | 1 / 1 |
| b: scroll by a page | 1.5 | **2.0** (median 3.3) | 296 | 944 | 0.065 | 0.066 | 3.0% | 11 / 11 |
| c: fling frame (Placeholders) | 1.4 | **2.2** (median 4.0) | 296 | 856 | 0.064 | 0.056 | 3.9% | 11 / 11 |
| c2: fling settles | 1.5 | **2.2** (median 4.3) | 296 | 944 | 0.059 | 0.057 | 3.8% | 11 / 11 |
| e: live redraw (every painted row new) | 3.2 | **3.3** (median 4.8) | 296 | 944 | 4.634 | 4.376 | 0.1% | 11 / 11 |

ExPivot's report paints one text column, so (A) costs it a few hundred bytes and about a
microsecond a paint. Row renders and mounts are identical, run by run.

**The whole live redraw** — answer, cube, report, Show and the grid's render, 82.6 MB allocated —
does not move at the least: 61.1 / 62.0 ms in round 1, 65.3 / 68.1 in round 2, and 65.7 / 64.7 in a
third round. Its median was 4.6–6.7 ms higher in V1 in all three rounds (63.3 / 70.0, 66.6 / 71.2,
67.9 / 73.2), while the grid's render was lower.
- The third round also counted the collector inside the redraw (`raw/core-pivot-v*-r3-gc.json`).
  The same collections, 5 of gen 0 and 2 of gen 1, with pauses of median 19.6 ms in V0 and 27.4 ms
  in V1. The difference in the median is therefore the collector's pauses, not the paint.
- Why V1's pauses are longer is measured in "Follow-up: why V1's ExPivot redraw is slower at the
  median" below.

## WebAssembly, in the page: the same grid

`/paint-cost`, the published app, the least and median of 15 runs, four runs interleaved
(`raw/browser-paintcost.json`). V0's `NotePaint` is under the batch method's noise (about ±5 µs),
so it is given as its median. All four runs were clean at their checks, and no console message was
logged.

| Scenario | `NotePaint` V0 (runs 1 / 4), median | **`NotePaint` V1, ms** (runs 2 / 3), median (least) | Bytes V0 | Bytes V1 | Step V0, ms, least (median) | Step V1, ms, least (median) | V1's paint, share of its step (median) |
|---|---|---|---|---|---|---|---|
| a: scroll by 1 row | 5 / 5 µs | **0.030 / 0.030** | 344 | 1,688 | 1.0 / 0.9 (1.3 / 1.3) | 1.0 / 0.9 (1.3 / 1.3) | 2% |
| b: scroll by a page | 5 / 5 µs | **0.445 / 0.455** (0.445 / 0.435) | 344 | 40,768 | 7.8 / 7.4 (9.1 / 9.1) | 8.0 / 8.0 (10.2 / 9.1) | 4% |
| c: fling frame (Placeholders) | 5 / 5 µs | **0.010 / 0.015** | 344 | 1,736 | 1.3 / 1.3 (1.6 / 1.6) | 1.2 / 1.3 (1.6 / 1.5) | 1% |
| c2: fling settles (full slice painted) | 5 / 5 µs | **0.470 / 0.460** (0.420 / 0.445) | 344 | 40,768 | 6.5 / 6.7 (7.0 / 7.0) | 7.2 / 6.9 (7.7 / 7.6) | 6% |
| d: horizontal scroll to a new slice | 5 / 5 µs | **0.515 / 0.515** (0.490 / 0.505) | 344 | 51,368 | 6.0 / 6.1 (6.7 / 6.4) | 6.5 / 6.6 (7.1 / 6.9) | 7% |
| e: live, 0 painted rows new | 5 / 5 µs | **0.005 / 0.005** (no new paint) | 0 | 0 | 7.4 / 7.5 (7.9 / 8.0) | 7.5 / 7.5 (7.8 / 7.9) | 0% |
| e: live, 3 painted rows new | 5 / 5 µs | **0.040 / 0.040** | 344 | 3,560 | 5.5 / 5.5 (5.8 / 5.6) | 5.3 / 5.5 (5.7 / 5.7) | 1% |
| e: live, every painted row new | 5 / 10 µs | **0.450 / 0.450** (0.435 / 0.445) | 344 | 40,696 | 8.3 / 8.0 (8.6 / 8.6) | 8.5 / 8.3 (8.7 / 8.7) | 5% |

- **In WebAssembly a full paint of 798 cells costs 0.44–0.52 ms**, about 0.6 µs a cell — nine to
  ten times CoreCLR's — and 41–51 KB. The steps that carry it rise by about that much (c2: 6.5–6.7
  → 6.9–7.2 ms at the least; d: 6.0–6.1 → 6.5–6.6), and the steps that do not, do not.
- **Scaled up** (an extrapolation, not a measurement): a screen of 80 rows × 40 columns repainted
  whole would cost about 2 ms and 160 KB a paint in WebAssembly.
- **The bytes are a garbage collector's matter.** Paging at 60 frames a second, the 41 KB a full
  paint would be about 2.4 MB a second more for Mono's collector, which stops the world. No long
  task and no frame over 33 ms was seen in any run below, but a collection's pause was not
  measured on its own.

## Browser: scripted scrolling

The main thread's task time per step, the least, median and most of 5 runs, and the frame intervals
(median of the runs' p50 / p95 / most). The frame rate is capped at 60 Hz, and every scenario's
work fits inside a frame in both variants, so frame intervals cannot show a difference this small.
Task time is the sensitive reading. Runs 1–4 are round 1, runs 5–8 round 2.

**`/grid-live-local`**, 18 painted rows × 11 columns. Run number: least / median / most task ms per step. Runs marked \* were overlapped by another agent's work and are not read.

| Scenario | V0 runs | V1 runs | Frames, V0 clean runs | Frames, V1 clean runs |
|---|---|---|---|---|
| one row a frame | 1: 3.33 / 3.49 / 3.71<br>4: 3.31 / 3.46 / 3.63<br>5\*: 3.20 / 3.30 / 3.78<br>8: 3.41 / 3.56 / 3.87 | 2\*: 3.32 / 3.46 / 3.77<br>3\*: 2.81 / 2.96 / 3.68<br>6: 3.47 / 3.62 / 3.81<br>7: 3.39 / 3.53 / 3.87 | 16.7 / 18.5 / 19.8<br>16.7 / 18.3 / 19.6<br>16.6 / 17.6 / 19.4 | 16.7 / 18.0 / 20.6<br>16.7 / 18.1 / 20.2 |
| one page a frame | 1: 5.43 / 5.46 / 6.02<br>4: 5.11 / 5.48 / 6.09<br>5\*: 5.45 / 5.68 / 6.05<br>8: 5.49 / 5.57 / 6.11 | 2\*: 5.30 / 5.47 / 6.01<br>3\*: 4.51 / 4.61 / 5.29<br>6: 5.63 / 5.65 / 6.25<br>7: 5.56 / 5.66 / 6.42 | 16.7 / 18.9 / 22.9<br>16.7 / 18.6 / 21.6<br>16.7 / 19.0 / 23.1 | 16.7 / 17.7 / 23.3<br>16.7 / 18.9 / 22.9 |
| fling, three pages a frame, and its settle | 1: 3.94 / 4.02 / 4.13<br>4: 3.88 / 3.96 / 4.19<br>5\*: 3.91 / 4.12 / 4.21<br>8: 3.48 / 4.06 / 4.20 | 2\*: 2.85 / 4.00 / 4.18<br>3\*: 4.00 / 4.19 / 4.29<br>6: 4.08 / 4.26 / 4.35<br>7: 3.56 / 3.96 / 4.05 | 16.7 / 18.5 / 19.0<br>16.7 / 18.4 / 19.8<br>16.7 / 18.5 / 19.6 | 16.7 / 17.5 / 18.5<br>16.7 / 18.6 / 20.7 |

**`/wide`**, 22 painted rows × 10 columns. Run number: least / median / most task ms per step. Runs marked \* were overlapped by another agent's work and are not read.

| Scenario | V0 runs | V1 runs | Frames, V0 clean runs | Frames, V1 clean runs |
|---|---|---|---|---|
| one row a frame | 1: 3.21 / 3.41 / 3.61<br>4\*: 3.11 / 3.32 / 3.47<br>5\*: 3.45 / 3.49 / 3.72<br>8: 3.34 / 3.52 / 3.66 | 2: 3.34 / 3.41 / 3.67<br>3\*: 3.17 / 3.43 / 3.62<br>6: 3.43 / 3.59 / 3.84<br>7: 3.37 / 3.55 / 3.85 | 16.7 / 18.5 / 21.6<br>16.7 / 17.8 / 20.2 | 16.7 / 18.5 / 20.5<br>16.7 / 17.7 / 20.9<br>16.7 / 17.7 / 20.6 |
| one page a frame | 1: 6.19 / 6.23 / 6.70<br>4\*: 6.17 / 6.25 / 6.73<br>5\*: 6.21 / 6.51 / 6.78<br>8: 6.23 / 6.54 / 6.86 | 2: 6.27 / 6.44 / 6.77<br>3\*: 5.71 / 6.16 / 6.43<br>6: 6.38 / 6.63 / 7.13<br>7: 6.28 / 6.56 / 7.06 | 16.7 / 18.7 / 23.0<br>16.7 / 18.2 / 22.3 | 16.7 / 18.5 / 24.8<br>16.7 / 19.1 / 23.0<br>16.6 / 19.2 / 21.9 |
| fling, three pages a frame, and its settle | 1: 3.06 / 3.30 / 3.63<br>4\*: 2.48 / 2.75 / 2.80<br>5\*: 2.85 / 3.51 / 3.70<br>8: 3.47 / 3.65 / 3.77 | 2: 3.16 / 3.34 / 3.39<br>3\*: 3.10 / 3.37 / 3.46<br>6: 3.27 / 3.29 / 3.53<br>7: 2.97 / 3.41 / 3.66 | 16.7 / 18.8 / 20.7<br>16.6 / 18.4 / 20.9 | 16.7 / 18.7 / 20.7<br>16.6 / 19.0 / 20.6<br>16.7 / 18.9 / 20.6 |
| a new column slice every frame | 1: 4.86 / 4.96 / 5.10<br>4\*: 4.54 / 4.76 / 4.86<br>5\*: 4.70 / 4.91 / 5.18<br>8: 4.94 / 5.03 / 5.11 | 2: 4.41 / 4.75 / 5.18<br>3\*: 4.88 / 5.01 / 5.11<br>6: 5.07 / 5.21 / 5.42<br>7: 5.13 / 5.15 / 5.30 | 16.7 / 18.8 / 21.1<br>16.7 / 17.2 / 22.1 | 16.7 / 18.6 / 20.5<br>16.7 / 18.4 / 22.4<br>16.7 / 18.5 / 21.2 |

- **No long task** (over 50 ms) and **no frame over 33.4 ms** in any run, clean or not. **No
  console error or warning** in any run.
- At these sizes (198 and 220 painted cells), the in-page measurement predicts about 0.11–0.12 ms
  more per step for V1 where every painted row is new (a page, a new slice). In the clean runs, V1's
  medians sit 0.1–0.2 ms above V0's on `/grid-live-local`'s page step (5.65, 5.66 against 5.46,
  5.48, 5.57). On `/wide` they fall among V0's (page: 6.44, 6.63, 6.56 against 6.23, 6.54; new
  slice: 4.75, 5.21, 5.15 against 4.96, 5.03).
  - That is the predicted amount, at the size of the spread between runs. One row a frame and the
    fling do not move.
- The frame intervals are the same: p50 16.7 ms everywhere, p95 17–19 ms, the most 19–25 ms, in
  both variants.

## Browser: a live update at 10⁶ rows, 1,000 changes a batch

`/grid-live-local?rows=1000000&batch=1000&interval=0`, Apply to the painted frame, ticket 01's
method (`raw/browser-live.json`). All four runs were clean at their checks. Every batch changed
the grid. No console message was logged.

| Run | Applied, ms: least / median / most | Apply → frame | **Apply → painted** | Longest task, median / most |
|---|---|---|---|---|
| 1, V0 | 16.8 / 29.5 / 88.0 | 16.9 / 29.6 / 88.2 | **17.6 / 30.6 / 89.1** | 0 / 94 |
| 2, V1 | 21.1 / 31.7 / 91.2 | 21.2 / 31.7 / 91.3 | **22.7 / 32.8 / 92.4** | 0 / 97 |
| 3, V1 | 22.0 / 29.3 / 87.7 | 22.1 / 29.4 / 87.8 | **23.0 / 30.2 / 88.8** | 0 / 94 |
| 4, V0 | 22.9 / 31.6 / 86.5 | 22.9 / 31.8 / 86.6 | **24.0 / 32.8 / 87.5** | 0 / 92 |

The two variants interleave. Ticket 01 measured 22.7 / 28.6 for the same configuration. The live
update's own cost — the source, its publication, the grid taking the Window in — is far larger than
a paint of 18 × 11 cells.

## Memory

**Browser, the out-of-memory loop** (`raw/oom-*.log`). The managed heap after a full, compacting
collection, and WebAssembly's linear memory:

| Variant, size | After loading | After redraw 1 / 2 / 3 / 4 | Redraw 5 to the last | Reports alive at the end | Linear memory | Verdict |
|---|---|---|---|---|---|---|
| V1, 1,000 × 100, 12 redraws | 87.9 MB | 129.1 / 172.3 / 215.5 / 225.2 | **225.2 MB, flat** | 4 of 13 | 495 MB, flat from redraw 4 | no out of memory |
| V1, 1,000 × 400, 15 redraws | 323.6 MB | 475.1 / 626.5 / 626.5 / 626.5 | **626.5–662.7 MB, flat** | 3 of 16 | 1,287 MB from redraw 3, 1,400 from redraw 9 | no out of memory |
| V0, 1,000 × 100, 12 redraws (this session) | 87.9 MB | 129.1 / 172.3 / 215.5 / 258.8 | **+41.1 MB a redraw**, 587.9 MB at 12 | 13 of 13 | 199 MB at load, 984 MB at redraw 12, still rising | no out of memory in 12 redraws; growing as in today's run, which ran out |

**CoreCLR, ticket 01's `pivot-memory`**, 1,000 × 100, one change a redraw, 20 redraws
(`raw/core-pivot-memory-*.json`): V0's heap climbed to 1,167–1,331 MB with every earlier report
alive (20 of 20). V1's settled at 314.5–348.2 MB from redraw 5, with 4 earlier reports alive.

- The paints were what pinned the generations. In V1 the newest paint holds the current report's
  rows only.
- **Three or four earlier reports stay alive in V1, held by something other than the paints.** The
  brief's run with the paints capped at 2 plateaued at the same 225 MB, and the loop's own probe
  shows four snapshots alive in both variants. Whatever holds them — the source, ExPivot's history —
  is not the grid's paints, and was not investigated here.

## Follow-up: why V1's ExPivot redraw is slower at the median

Asked after the first report. On CoreCLR the whole live redraw (1,000 × 100, 101,001 report rows)
had the same collections in both variants, 5 of gen 0 and 2 of gen 1, but pauses of median 19.6 ms
in V0 against 27.4 ms in V1. Three hypotheses were tested, each against a prediction that would
confirm or kill it, with the same harness, contention checks and constraints.

**Method.** The redraw (scenario e) was run alone (`pivot-gc` and `pivot-steady` in
`spikes/paint-text/PaintCosts`, `PivotGc.cs`), 15 runs after 3 untimed. Every collection inside a
timed redraw was read from the runtime's own GC events through an in-process `EventListener`
(`GcRecorder.cs`: Microsoft-Windows-DotNETRuntime, GC keyword, Verbose), and `GC.GetGCMemoryInfo` was
read before and after. The events give, per collection:
- generation and type (blocking, background, foreground) and reason;
- duration, and the pause from SuspendEE to RestartEE;
- `GlobalMechanisms`, whose 0x2 bit means it compacted, and the compact reason
  (`CompactMechanisms`, decoded in the order of the runtime's `gc_heap_compact_reason`:
  low_ephemeral = 0, high_frag = 1, …);
- how its survivors were placed (`FreeListAllocated`, `EndOfSegAllocated`) and what each generation
  promoted.

The listener costs time — the redraw outside the collector's pauses is about 6 ms longer for V0
and 2 ms for V1 with it — but the same in every run of a variant. Every run here was clean at its
checks.

| Run (V0, V1, V1, V0; then one of each) | Redraw, ms: least / median / most | GC pause, median | Collections gen0+1+2 | Compacting collections (runs that had one) | Pause in compacting / sweeping collections, median ms per redraw | Promoted into a free list / at the end of a segment, MB per redraw | Gen 1 promoted, MB | Gen 2 after, MB | Committed before → after, MB | Working set Δ |
|---|---|---|---|---|---|---|---|---|---|---|
| full pre-collection, V0 (1) | 70.3 / 72.9 / 84.0 | 18.1 | 3+2+0 | 10 (6 of 15) | 0.0 / 17.0 | 5.8 / 0.0 | 24.9 | 476 | 722 → 780 | +53 MB |
| full, V1 (2) | 65.2 / 75.1 / 83.5 | 26.9 | 3+2+0 | **29 (14 of 15)** | 17.7 / 9.6 | **24.9** / 0.0 | 24.9 | 230 | 422 → 429 | +11 MB |
| full, V1 (3) | 65.5 / 74.9 / 83.3 | 26.8 | 3+2+0 | **29 (14 of 15)** | 17.9 / 9.1 | **24.9** / 0.0 | 24.9 | 230 | 422 → 429 | +11 MB |
| full, V0 (4) | 67.2 / 72.8 / 83.2 | 17.6 | 3+2+0 | 10 (6 of 15) | 0.0 / 16.3 | 5.8 / 0.0 | 24.9 | 476 | 722 → 779 | +53 MB |
| compacting pre-collection, V0 | 77.3 / **88.0** / 107.0 | 34.6 | 3+2+0 | 5 (2 of 15) | 0.0 / 33.4 | 1.5 / 0.1 | 24.9 | 420 | 655 → 718 | +61 MB |
| compacting pre-collection, V1 | 75.5 / **76.2** / 79.5 | 27.4 | 3+2+0 | **0** | 0.0 / 27.6 | 1.5 / 0.0 | 24.9 | 219 | 357 → 400 | +41 MB |
| `DOTNET_GCRetainVM=1`, V0 | 65.2 / 69.0 / 79.6 | 16.6 | 3+2+0 | 10 (6 of 15) | 0.0 / 16.2 | 5.9 / 0.0 | 24.9 | 476 | 722 → 780 | +53 MB |
| `DOTNET_GCRetainVM=1`, V1 | 65.4 / 74.8 / 86.2 | 26.8 | 3+2+0 | 29 (14 of 15) | 18.0 / 9.1 | 24.9 / 0.0 | 24.9 | 230 | 422 → 429 | +11 MB |

What the collections show, in the base setting (a full, non-compacting collection before each run,
as the rig makes it):

- **Same garbage, same collections.** Both variants allocate 82.6 MB per redraw, promote 24.9 MB
  out of gen 1, and run three gen-0 and two gen-1 collections.
- **V1's gen-1 collections compact; V0's sweep.**
  - V1: 29 of its 31 gen-1 collections compacted, 26 of them for the reason high_frag. Median pause
    9.0 ms against 4.5 ms for one that sweeps.
  - V0: 10 of 32 compacted (low_ephemeral 6, high_frag 4). The rest swept, median 4.7 ms.
  - Two compacting gen-1 collections per redraw are V1's extra ≈ 9 ms of pause: 26.9 against
    18.1 ms. The first gen-0 collection of a redraw is also 1.7–2.2 ms longer in V1 (median
    5.6–5.9 against 3.7–3.9 ms).
- **Where the survivors go.**
  - V1 places all of gen 1's survivors in gen 2's free list: 24.9 MB per redraw, and gen 2 does
    not grow (230 MB).
  - V0 places 5.8 MB there, and its free-list records show many rejections. Gen 2 grows by about
    16 MB at each gen-1 collection instead (443 → 459 → 476 MB within a redraw).
  - V1's full collection freed the previous generations inside gen 2. Gen 2 after it: 221 MB with
    34 MB free (15%), against V0's 444 MB with 55 MB free (12.5%). The next redraw's gen-1
    collections compact their survivors into that free space.
- **Outside the collector, V1 is faster.** The redraw less its GC pauses has a median of 47.2–47.3
  ms in V1 against 53.1–54.0 in V0 (45.6 against 47.7 without the listener). V0 commits fresh memory
  every redraw (+58 MB committed, +53 MB working set) because it never frees any. That offsets most
  of the pause difference: the redraw medians differ by only 1.9–2.3 ms here, 5.3 ms without the
  listener.

**H1, fragmentation and promotion into free lists — held, in a sharper form.**
- What held: in V1, gen 1's survivors are promoted into gen 2's free list (24.9 against 5.8 MB per
  redraw), with similar promoted bytes (24.9 MB in both) and longer pauses. The slow part is that
  those gen-1 collections **compact** — two per redraw, about 9 ms each against about 4.6 ms when
  sweeping — not the free-list allocation itself.
- What did not: V1 does *not* end a redraw with more `FragmentedBytes` (30.3 against 64.5 MB). It
  ends with less, because it compacted.
- A compacting pre-collection leaves no free list to fill. Then V1's gen-1 collections never
  compact (0 of 30), and **the gap reverses**: V1 76.2 against V0 88.0 ms at the median. After a
  compacting collection, the first gen-0 collection of the next redraw takes 15–21 ms (median) in both
  variants, and V0 still compacts some gen-1s.

**H2, commit/decommit — killed.**
- V1 commits almost nothing inside a redraw (422 → 429 MB). V0 commits 58 MB, the opposite of the
  prediction.
- `DOTNET_GCRetainVM=1` leaves V1 unchanged (74.8 ms median, 26.8 ms pause, 29 compacting gen-1s).
  It brings V0 down a little (69.0 ms), outside the pauses.

**H3, a short-window artifact — killed as stated; the cost moves rather than disappears.** 60
consecutive live redraws, no forced collection anywhere, then 150 to go past the 64 kept paints
(`raw/core-steady-*.json`, `raw/core-steady150-*.json`). Redraws 1–9 are identical in the two
variants (no gen-2 collection has reclaimed anything yet), and they part at redraw 10:

| Redraws | V0: median / mean / most, ms | V0: background gen-2 GCs | V0: heap after | V1: median / mean / most, ms | V1: background gen-2 GCs | V1: heap after |
|---|---|---|---|---|---|---|
| 11–60 (run of 60) | 80.9 / 91.5 / 202.5 | 2 (at 20 and 39) | 3,960 MB at 60 | **97.5 / 107.6 / 143.7** | 24 | 686 MB |
| 11–60 (run of 150) | 77.0 / 87.2 / 191.7 | 2 | 3,961 MB | 96.4 / 107.2 / 145.9 | 24 | 685 MB |
| 61–70 | 81.5 / 82.2 / 96.9 | 0 | 4,758 MB | 109.9 / 111.6 / 144.0 | 6 | 704 MB |
| 71–100 | 83.1 / 105.4 / 207.2 | 1 | 5,778 MB | 104.5 / 108.5 / 148.1 | 14 | 695 MB |
| 101–150 | 84.7 / 97.4 / 205.6 | 1 | **8,088 MB** | 97.9 / 107.7 / 146.9 | 24 | 698 MB |

- **In steady state V1 is the slower of the two at the median on CoreCLR**, by 13–28 ms (16–35%),
  and at the mean by 3–29 ms.
  - It runs a background gen-2 collection every second redraw to take back the generation it let
    go. Each brings foreground and compacting gen-1 collections: the redraw alternates about 77,
    92 and 125–145 ms.
  - Its GC pause sum is 1,716–1,732 ms per 50 redraws against 1,294–1,522 for V0.
- **V0's median creeps up only from 77 to 85 ms over 150 redraws** and stays below V1's, so H3's
  prediction fails. What V0 pays instead:
  - **memory**: 4.0 GB of heap at 60 redraws and 8.1 GB at 150. The generations past the 64 kept
    paints are garbage that no gen-2 collection comes for;
  - **its spikes**: when a background collection finally runs over a multi-gigabyte heap, five
    redraws in a row take 180–207 ms (at 40–42, 78–82 and 134–138). V1's worst redraw is 148 ms.
  - Its median is lower because it defers that collection work, not because it avoids it.

**Browser, Mono WebAssembly (no background GC)**, 1,000 × 100, 15 consecutive redraws, no forced
collection, Apply → the frame that shows the report (`harness/scripts/browser-pivot.mjs`, ticket
01's method; `raw/browser-pivot.json`), runs interleaved V0, V1, V1, V0, all clean, no console
message:

| Run | Apply → painted, ms: least / median / most | Linear memory after redraw 1 → 15 |
|---|---|---|
| 1, V0 | 544 / 579 / 646 | 239 → 1,167 MB (+96 MB about every redraw from the 5th) |
| 2, V1 | 547 / 576 / 585 | 239 → 591 MB (flat at 495 from redraw 4) |
| 3, V1 | 544 / 579 / 585 | 239 → 593 MB (flat at 495 from redraw 4) |
| 4, V0 | 552 / 584 / 649 | 239 → 1,170 MB |

- The OOM loop as you ran it, 15 redraws, its own `s` per step (`raw/oom-v*-1000x100-15.log`):
  V0 0.761–0.833 s, median 0.794; V1 0.761–0.833 s, median 0.767 (the first step, 0.644–0.645 s in
  both, is in the medians). Your runs gave V0 0.78–0.82 and paints capped at 2 0.76–0.80.
- **In the browser V0 is not faster.** The medians are the same within 3–8 ms, and V0's slowest
  redraws are 60 ms slower (646–649 against 585). Its memory grows until it runs out.

**Answer.** On CoreCLR, V0's redraw is faster at the median because of the heap the harness hands
each redraw.
- In V1 the full collection before each run frees the previous generations inside gen 2, and the
  next redraw's gen-1 collections compact their survivors into that free space. That is two
  compactions of about 9 ms where V0 sweeps in about 4.6 ms, so ≈ 9 ms more pause.
- V0 instead commits fresh memory every redraw, which costs it 2–7 ms outside the pauses. The
  median gap is the difference, 2–5 ms.
- Compacting the heap first reverses it (V1 76 against V0 88 ms). Retaining virtual memory does not
  change it.
- None of it is the paint: the paint costs ExPivot microseconds.
- **In steady state it matters on CoreCLR, the other way round from H3's prediction.** V1 is slower
  at the median (96–110 against 77–85 ms), because it collects gen 2 every other redraw to keep its
  heap at 0.7 GB. V0 skips that work by keeping garbage: 8 GB after 150 redraws, and five-redraw
  runs of 180–207 ms whenever a collection finally comes.
- **In the browser it does not matter.** Mono has no background collector, the medians are equal
  (576–579 against 579–584 ms), V1's slowest redraw is 60 ms better, and V0 grows its linear
  memory by about 96 MB a redraw until it runs out.

## Contention log

`raw/contention.log` has every run's checks; `raw/contended-runs.txt` lists the runs that another
agent's work overlapped:

- Round 1 checked only before each run. Its after-checks, read afterwards, showed five runs
  overlapped:
  - `pivot-v1` on CoreCLR: another Playwright Chrome as it ended;
  - two `/grid-live-local` scroll runs (V1): the Codex track's `dotnet test` starting about 2 s
    before the end of one, another Playwright Chrome as the other ended;
  - two `/wide` scroll runs (V1 and V0): the Codex track's layer-3 run throughout one.
- Those batches were run again with checks before and after (round 2), and again two runs were
  overlapped at their end (one V0 run on each page, flagged by the wrapper). Every overlapped run is
  marked in the scroll tables and not read; each page still has two or three clean runs of each
  variant. ExPivot's round 2 was clean, and so was its third round.
- The CoreCLR grid, the in-page WebAssembly and the live update runs were clean at every check, and
  so was every run of the follow-up (CoreCLR `pivot-gc` and `pivot-steady`, and the browser's
  ExPivot redraws). The OOM loop's runs waited for idle before starting but have no check after. The
  memory runs are not timings; they waited for idle before starting.

## Reproducing it

From this worktree, at `41c8d8c8` with `prototype.patch` applied for V1 (and not for V0):

```sh
git apply verification/2026-10-06-macos-paint-text-cost/harness/harness.patch   # spikes/paint-text, the two harness pages, GridLiveLocalPage's ?step=1
git add -N spikes/paint-text samples/ExGrid.DemoPages/Pages/PaintCostPage.razor samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor
nix develop -c dotnet build -c Release spikes/paint-text/PaintCosts/PaintCosts.csproj
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll grid  <out.json> 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll pivot <out.json> 1000 100 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll pivot-memory <out.json> 1000 100 1 20
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll pivot-gc <out.json> 1000 100 3 15 full verbose   # or compact; DOTNET_GCRetainVM=1 for H2
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll pivot-steady <out.json> 1000 100 150
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o <host>
node tests/ExGrid.Browser/static-host.mjs <host>/wwwroot 5799 &
node harness/scripts/browser-paintcost.mjs http://localhost:5799 <out.json> <label> 15 3
node harness/scripts/browser-scroll.mjs    http://localhost:5799 <out.json> <label> 5 1 live   # or wide
node harness/scripts/browser-live.mjs      http://localhost:5799 <out.json> <label> 1000000 1000 15 3
node harness/scripts/browser-pivot.mjs     http://localhost:5799 <out.json> <label> 1000 100 15
```

The browser scripts load Playwright from another worktree's `tests/ExGrid.Browser/node_modules`
(their first lines). `harness/scripts/` also holds the wrappers that ran the batches, with this
machine's paths.

## Verdict

(A) costs a paint the text of the rows it brings into view. On a 42 × 19 grid that is about 50 µs
on CoreCLR and **about half a millisecond in WebAssembly** for a paint that brings every painted row
(a page scroll, a fling's settle, a new column slice, a live update that replaces every painted
row), and 41–67 KB. Next to it, the step that carries it costs 6–10 ms in WebAssembly (4–7%) and
0.3–0.4 ms on CoreCLR (13–16% of the render). A paint that brings a few rows costs a few µs, a fling
frame almost nothing, and ExPivot's paints about a microsecond.

Row renders are identical and nothing new reaches JavaScript. In the browser, scripted scrolling on
two pages and a live update at 10⁶ rows show no difference between V0 and V1 beyond run-to-run
spread, no long task and no slow frame. The page step on `/grid-live-local` sits the predicted
0.1–0.2 ms higher.

A user would not notice it at these sizes. Two things would grow it:
- a much larger painted area (an extrapolated 2 ms a full paint at 80 × 40 cells in WebAssembly);
- the extra allocation, whose collector pauses were not measured on their own.

What (A) does buy is measured: ExPivot's live memory stops growing (225 MB flat at 101,001 rows;
627–663 MB flat at 401,001, where V0 ran out of memory).

Its cost is not in time but in meaning. As briefed, with no row or key in an older paint, it cannot
keep ADR-0142's key pairing across an order change, the instance a told press's refusal carries,
or, with no row left at the pressed position, the press at all. That is the user's decision
(options above).
