# Aligned blocks beside rows either side: a remote report's Window read ahead as ag-grid reads it (ticket 14)

Date: 2026-10-11, from 06:54 UTC to 08:38 UTC. `lab/remote-read-ahead` at `21d7a94`, a lab branch that is never
merged: three commits on `f4ec680`, the first record's. This continues
[the first record](../2026-10-10-linux-remote-read-ahead/README.md) of
[ticket 14](../../docs/specs/live-data/issues/14-a-remote-reports-window-is-read-ahead.md). It keeps that record's
harness, its pace and its three layouts. Since that record, the user has decided two things (the ticket's comment of
2026-10-11):
- a question whose Window covers the range stays in flight;
- the number lives on `PivotReportSource.Fetch`.

The default is not decided. The user asked how ag-grid compares, and asked for aligned blocks, as ag-grid's
Server-Side row model fetches them, to be measured beside N rows either side. This record is that measurement.

It decides nothing. Performance never gates (AGENTS.md), and every time here is observational. Counts and bytes
are exact. The raw files are in [`raw/`](raw/), one directory a pass of the run:
- the per-sample files are gzipped JSON, read with `zcat`;
- the `*-summary.json` files are the run's own summaries.

[`tables.py`](tables.py) prints every table below from the per-sample files.

## What ag-grid does

ag-grid at `latest`, 12c6bb02b11f2e7f2b6451f6bbb9ee267339603b:
- **Its defaults** are in `packages/ag-grid-community/src/gridOptionsDefault.ts`:
  - `cacheBlockSize` = 100: the Server-Side and Infinite row models fetch rows in blocks of 100;
  - `rowBuffer` = 10: "the number of rows rendered outside the viewable area";
  - `maxBlocksInCache` = -1: every block asked for is kept;
  - `maxConcurrentDatasourceRequests` = 2 and `blockLoadDebounceMillis` = 0;
  - `viewportRowModelBufferSize` = 5 and `viewportRowModelPageSize` = 5, for the Viewport row model.
- **A block is asked for before the Viewport reaches it.**
  - The row renderer renders the rows on screen and `rowBuffer` rows' height either side
    (`workOutFirstAndLastRowsToRender` in `packages/ag-grid-community/src/rendering/rowRenderer.ts`).
  - In the Server-Side row model, a rendered row whose block is not held is a stub. The block loader asks for the
    block of a stub among the rendered rows (`lazyCache.ts` and `lazyBlockLoadingService.ts` in
    `packages/ag-grid-enterprise/src/serverSideRowModel/stores/lazy/`). The Infinite row model creates the block when
    the renderer asks for its row (`getRow` in `packages/ag-grid-community/src/infiniteRowModel/infiniteCache.ts`).
  - Blocks are aligned: a row's block starts at its index less its index modulo `cacheBlockSize`.
  - So, scrolling down, the next block is asked for about 10 rows before the Viewport reaches it, and the block the
    Viewport leaves is kept.
- **The Viewport row model** asks its datasource for the rendered rows and `viewportRowModelBufferSize` either side,
  rounded out to pages of `viewportRowModelPageSize`, whenever that range changes (`viewportRowModel.ts` in
  `packages/ag-grid-enterprise/src/viewportRowModel/`). Its datasource pushes the rows. It is not measured here.

## What was built, and what could not be

The lab branch adds two modes beside the first record's N rows either side:
- **`b50`, `b100` and `b200`: aligned blocks** (`25005b1`).
  - ExPivot asks for the aligned blocks of B rows that cover the range the grid needs and 10 rows either side,
    clamped into the report: one block, or two at a boundary.
  - Scrolling on, it is two: the block the Viewport leaves and the one it enters. From row 286, say, blocks of 100
    ask for rows 200 to 399.
  - The decided rule holds: a question whose Window covers the range stays in flight.
- **`b100t10` and `60t10`: asked before the Window runs out** (`a657b79`, with its rule for a question in flight
  corrected in `21d7a94`).
  - A lab switch in ExGrid, `RangeMarginRows`, raises the Range Request once the rows on screen and 10 either side
    are not all in the Window. It still asks for the rows on screen.
  - A question in flight stays while its Window covers the rows on screen, the decided rule. The 10 rows are asked
    of the Window held, so once the question lands, the grid asks again if they are still not all in it.
  - `b100t10` is ag-grid's rule over one Window. `60t10` is 60 rows either side asked 10 rows early: the trigger
    measured apart from the blocks.

Two things ag-grid does are not reproduced:
- **ag-grid keeps every block; ExPivot's report source keeps one contiguous Window.** A Window replaces the Window
  before it. So the block the Viewport leaves is asked for again inside the next Window, and a block scrolled back to
  is asked for again. ag-grid's "every block kept" is not reproduced, and no block cache was built.
- **ag-grid asks before the Window runs out; ExPivot cannot without a change to ExGrid.**
  - ExGrid raises its Range Request "when the rows on screen are not all in the Window" (`OnRangeNeeded`), and asks
    for the visible range: under ADR-0001, read-ahead "is the Consumer's judgement, not something the grid can
    guess". That request is the first ExPivot hears of the range.
  - So in `b100` the 10 rows widen the Window asked at the edge, and ask nothing earlier.
  - Asking early took the lab's Range Request margin in ExGrid. **Adopting it would be a decision of its own**,
    against ADR-0001: the grid would raise its Range Request before the rows on screen leave its Window.

## Method

The first record's method, unchanged but for what follows. Its [Method](../2026-10-10-linux-remote-read-ahead/README.md#method)
says how the page, the three layouts, the samples, the inputs and their paces, the timing, the waits, the warm-ups,
the answers, the live updates and the kept Windows were measured.
- **Machine**: the same cloud container, restarted since the first record: Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB;
  Ubuntu 24.04.5, Linux 6.18; .NET SDK 10.0.203 through nix; Node 24.14.1, Playwright 1.62.1.
  - The load was 0.36 when the run began, once the idle build servers of the build and publish were stopped.
  - Nothing else ran on the container meanwhile.
- **The browser was Playwright's bundled Chromium 141.0.7390.37, headed under xvfb (2,560 × 1,600 × 24) on this
  Linux container, not branded Chrome or Edge**, which the container does not have. A local Playwright config, never
  committed, launched it in place of the channels. The page was 1,400 × 1,100. The console stayed clean in every
  test (CON-1 to CON-3).
- **The code**: the hosts were published in Release, as the first record's were, from the commit each pass names
  below.
  - The DemoHost was published as WebAssembly and served as static files by `static-host.mjs`.
  - The demo API server held 20,000 trades, and a million for the dense report.
  - `21d7a94` changed only what a t10 mode asks, so p1 and p2, run at `a657b79`, ask as `21d7a94` does.
- **The round trip**: `latency-proxy.mjs`, 150 ms added to each, as in the first record and ADR-0151's experiment.
- **Five passes**, each one run of `read-ahead-run.sh` with its own `raw/` directory:

  | Pass | Commit | Read-aheads | Layouts | Parts |
  |---|---|---|---|---|
  | p1 | `a657b79` | b100, and 60 again as the anchor | narrow, wide | scrolling, live updates |
  | p2 | `a657b79` | b50, b200 | narrow, wide | scrolling, live updates |
  | p3 | `21d7a94` | b100t10, 60t10 | narrow, wide | scrolling |
  | p4 | `21d7a94` | 30, 120 again | narrow, wide | scrolling, live updates |
  | p5 | `21d7a94` | b50, b100, b200, 30, 60, 120 | wide-dense, over a million trades | live updates |

  - The t10s are left out of live updates. A tick asks for the Window held, and the trigger changes only what a
    scroll asks.
  - N = 60 again shows how far this run's machine differs from the first record's. The first record's raw files
    give its numbers beside this run's.
- **The trigger's first run** was at `a657b79`. It superseded a question in flight once the rows on screen and 10
  either side left its Window. That is stricter than the decided rule.
  - Under held PageDown over the narrow report, 60t10 then superseded 8 of 9 questions, and left Placeholders on
    screen 84% of the time.
  - `21d7a94` keeps the question in flight while its Window covers the rows on screen, and the t10s ran again as p3.
  - The first run's files are in `raw/trial-a657b79`, and its tables are in [The trigger's first run](#the-triggers-first-run).

## In brief

- **Over one contiguous Window, aligned blocks are N rows either side by another name.**
  - Scrolling down, blocks of B rows ask once every B rows for two blocks: the one the Viewport leaves and the one it
    enters, 2B rows. N rows either side asks once every N + 1 rows for 2N rows and the Viewport.
  - So blocks fall where their Window's size puts them among the N either side. One at a time, ArrowDown met 9, 5
    and 3 Placeholder episodes in 480 rows at b50, b100 and b200, against 15, 7 and 3 at N = 30, 60 and 120.
  - Held, the wheel and ArrowDown showed Placeholders 15% and 6% of the time at b100 over the narrow report. That
    lies between N = 60's 19% and 8% and N = 120's 10% and 4%. Over the wide report it was 19% and 9%, between 23%
    and 10% and 13% and 5%.
  - The shape buys nothing at a given size. b100's 200 rows fell between N = 60's 135 and N = 120's 255, as their size
    puts them. b200's 400 rows did no better than N = 120's 255: 10% and 5% against 10% and 4% narrow, 16% and 7%
    against 13% and 5% wide.
  - An episode lasts a round trip and the reading of its answer, so a larger Window's lasts longer. One at a time,
    ArrowDown's lasted 240 ms at b200 against 189 ms at N = 60 narrow, and 381 ms against 239 ms wide.
- **Per row scrolled, blocks carry a little less, in fewer and larger questions.**
  - A Window asked overlaps the one held. Blocks answer 2.00 rows for each row scrolled; N = 30, 60 and 120 answer
    2.42, 2.21 and 2.11 (narrow).
  - b100 asks once per 100 rows, N = 60 1.5 times. Each answer is larger: 120 KiB of JSON against 83 narrow, 668
    against 462 wide, read in 35 and 80 ms against 25 and 54.
  - A block cache would halve what blocks carry, as ag-grid's does. One contiguous Window cannot keep one.
- **Asking before the Window runs out is what removes the Placeholders, blocks or not.**
  - The t10 modes ask once the rows on screen and 10 either side are not all in the Window, as ag-grid's rendered
    rows ask.
  - One at a time, the wheel and ArrowDown then met no Placeholder at b100t10 or at 60t10, over either report. Held
    ArrowDown met none either.
  - The held wheel fell from 15% to 4% at b100 and from 19% to 8% at N = 60 over the narrow report. Over the wide one
    it fell from 19% to 8% and from 23% to 13%.
  - Held PageDown, 11 to 13 rows a press, jumps past 10 rows. It showed Placeholders 58% and 49% of the time at
    b100t10, against 73% and 60% at b100. At N = 60 the trigger helped it over the wide report (56% against 69%),
    but not over the narrow one (78% against 73%), where 3 questions a sample were superseded.
  - Asked 10 rows early, each N = 60 Window reaches 10 rows less far, so N = 60 asks more often: 2.65 rows answered
    for each row scrolled against 2.21. b100t10 asks for b100's Windows, sooner.
  - **The early ask needs a change to ExGrid**, against ADR-0001: a Range Request raised before the rows on screen
    leave the Window. Adopting it would be a decision of its own.
- **A live update and the kept Windows grow with the Window's rows, whatever its shape.**
  - Over the wide report with 1,000-trade ticks, the server projected the Window in 138 ms an update at b100 (200
    rows) and 254 ms at b200 (400). That compares with 90 ms at N = 60 (133 rows) and 168 ms at N = 120 (253).
  - The two Windows kept held 1.1, 2.1, 0.7 and 1.3 MiB of the server's heap.
  - Over the dense report, b100's Window took 3.5 MiB of JSON (804 KiB on the wire), read in 618 ms on the browser's
    one thread, against N = 60's 2.3 MiB in 411 ms. Its two kept Windows held 11.3 MiB, against 7.5 MiB.
- **The anchor held where the pace did.**
  - N = 60 again showed held Placeholders 19% and 8% of the time (wheel, ArrowDown) over the narrow report, against
    19% and 7% in the first record.
  - Over the wide report it was 23% and 10% against 21% and 8%, with held inputs 46 ms apart against 55 ms then.
  - One input at a time, every N = 60 case was within 0.1 s of the first record.

## Placeholders while scrolling

The time rows not held were on screen, per 480 rows: the median of three samples, and in brackets the episodes and
one episode's median. "Held" gives the share of the scroll's time instead. "None" is a median of no episode.

### narrow (6 value columns)

| Input | b50 | b100 | b200 | 30 | 60 | 120 | b100t10 | 60t10 |
|---|---|---|---|---|---|---|---|---|
| Wheel, a notch at a time | 1.8 s (9 × 205 ms) | 1.1 s (5 × 238 ms) | 0.80 s (3 × 277 ms) | 2.7 s (14 × 186 ms) | 1.5 s (7 × 211 ms) | 0.73 s (3 × 253 ms) | none | none |
| Wheel, held | 24% | 15% | 10% | 36% | 19% | 10% | 4% | 8% |
| ArrowDown, a press at a time | 1.7 s (9 × 180 ms) | 0.98 s (5 × 196 ms) | 0.68 s (3 × 240 ms) | 2.6 s (15 × 174 ms) | 1.4 s (7 × 189 ms) | 0.67 s (3 × 209 ms) | none | none |
| ArrowDown, held | 11% | 6% | 5% | 18% | 8% | 4% | 0% | 0% |
| PageDown, a press at a time | 1.6 s (9 × 180 ms) | 0.99 s (5 × 200 ms) | 0.67 s (3 × 232 ms) | 2.1 s (12 × 175 ms) | 1.3 s (7 × 184 ms) | 0.65 s (3 × 213 ms) | 0.20 s (1 × 198 ms) | none |
| PageDown, held | 88% | 73% | 42% | 89% | 73% | 44% | 58% | 78% |

### wide (251 value columns)

| Input | b50 | b100 | b200 | 30 | 60 | 120 | b100t10 | 60t10 |
|---|---|---|---|---|---|---|---|---|
| Wheel, a notch at a time | 2.2 s (9 × 245 ms) | 1.5 s (5 × 313 ms) | 1.3 s (3 × 459 ms) | 3.2 s (14 × 225 ms) | 2.0 s (7 × 281 ms) | 1.0 s (3 × 354 ms) | none | none |
| Wheel, held | 28% | 19% | 16% | 40% | 23% | 13% | 8% | 13% |
| ArrowDown, a press at a time | 2.0 s (9 × 223 ms) | 1.4 s (5 × 287 ms) | 1.2 s (3 × 381 ms) | 3.2 s (15 × 208 ms) | 1.7 s (7 × 239 ms) | 0.96 s (3 × 317 ms) | none | none |
| ArrowDown, held | 13% | 9% | 7% | 20% | 10% | 5% | 0% | 0% |
| PageDown, a press at a time | 2.0 s (9 × 224 ms) | 1.4 s (5 × 281 ms) | 1.2 s (3 × 394 ms) | 3.0 s (14 × 210 ms) | 1.7 s (7 × 236 ms) | 1.2 s (4 × 303 ms) | none | none |
| PageDown, held | 89% | 60% | 42% | 90% | 69% | 43% | 49% | 56% |

Questions asked while held, and superseded (median of three samples):

| Report, input | b50 | b100 | b200 | 30 | 60 | 120 | b100t10 | 60t10 |
|---|---|---|---|---|---|---|---|---|
| narrow, Wheel | 9, 0 | 5, 0 | 3, 0 | 14, 0 | 7, 0 | 3, 0 | 5, 0 | 8, 0 |
| narrow, ArrowDown | 9, 0 | 5, 0 | 3, 0 | 15, 0 | 7, 0 | 3, 0 | 5, 0 | 9, 0 |
| narrow, PageDown | 9, 8 | 5, 1 | 3, 0 | 12, 11 | 7, 1 | 3, 0 | 5, 0 | 8, 3 |
| wide, Wheel | 9, 0 | 5, 0 | 3, 0 | 14, 0 | 7, 0 | 3, 0 | 5, 0 | 8, 0 |
| wide, ArrowDown | 9, 0 | 5, 0 | 3, 0 | 15, 0 | 7, 0 | 3, 0 | 5, 0 | 9, 0 |
| wide, PageDown | 9, 6 | 5, 0 | 3, 0 | 14, 13 | 7, 0 | 4, 0 | 5, 0 | 8, 0 |

- **Blocks ask the same Windows from every row of their blocks.** One at a time from row 200 over the narrow report,
  ArrowDown asked b100 for rows 200–399, 300–499, 400–599 and 500–699.
- **Each sample started on the Window asked when its start was placed**, and under blocks that depends on where the
  start falls against them.
  - From row 200, b100 held rows 100–299, 85 rows below the Viewport. From row 8,172 it held 8,100–8,199, 13 rows
    below; from row 16,143, 16,100–16,199, 42 below.
  - N = 60 held 60 rows below at every start.
  - So under blocks a sample met its first episode after 13 to 85 rows. The detail is in `tables.py`'s output.
- **Asked early, one input at a time met no Placeholder**: each input waited for a settled grid, and the question
  had landed by then. Only PageDown at b100t10, 11 to 13 rows a press, could jump past the 10 rows: it met one
  episode a sample over the narrow report, and one in three samples over the wide one.
- **Held PageDown outran every Window here**, at 240 to 400 rows a second over the narrow report and 150 to 330 over
  the wide one.
  - It was held best by the largest Windows, b200 and N = 120 (42–44%).
  - At b50 and N = 30 it crossed a Window within a round trip and superseded nearly every question: 8 of 9 and 11 of
    12 over the narrow report.
- **A held share depends on the pace the inputs achieved.**
  - Within this run, held paces were close across read-aheads for the wheel (50–54 ms apart) and ArrowDown (33–39
    ms). PageDown's were 32–42 ms apart narrow and 32–55 ms wide, where larger answers kept the browser's one thread
    busy longer.
  - Across records they differed. The first record's N = 120 PageDown presses came 60–64 ms apart, this run's
    38–43 ms. Three episodes a sample both times, of about 220 ms then and 250 ms now, were 27% of the scroll then
    and 44% now.

### N = 60, the first record and this run

| Report, input | 60, 2026-10-10 | 60, this run |
|---|---|---|
| narrow, Wheel, a notch at a time | 1.4 s (7 × 204 ms) | 1.5 s (7 × 211 ms) |
| narrow, Wheel, held | 19% | 19% |
| narrow, ArrowDown, a press at a time | 1.3 s (7 × 185 ms) | 1.4 s (7 × 189 ms) |
| narrow, ArrowDown, held | 7% | 8% |
| narrow, PageDown, a press at a time | 1.3 s (7 × 184 ms) | 1.3 s (7 × 184 ms) |
| narrow, PageDown, held | 72% | 73% |
| wide, Wheel, a notch at a time | 1.9 s (7 × 260 ms) | 2.0 s (7 × 281 ms) |
| wide, Wheel, held | 21% | 23% |
| wide, ArrowDown, a press at a time | 1.7 s (7 × 241 ms) | 1.7 s (7 × 239 ms) |
| wide, ArrowDown, held | 8% | 10% |
| wide, PageDown, a press at a time | 1.7 s (7 × 241 ms) | 1.7 s (7 × 236 ms) |
| wide, PageDown, held | 63% | 69% |

Held inputs came a median 42 ms apart then and now over the narrow report. Over the wide one they came 55 ms apart
then and 46 ms now.

### The trigger's first run

At `a657b79`, a question in flight was superseded once the rows on screen and 10 either side left its Window:

| Input | narrow, b100t10 | narrow, 60t10 | wide, b100t10 | wide, 60t10 |
|---|---|---|---|---|
| Wheel, held | 4% | 8% | 9% | 13% |
| ArrowDown, held | 0% | 0% | 0% | 0% |
| PageDown, held | 62% | 84% | 50% | 57% |
| PageDown held: questions, superseded | 5, 1 | 9, 8 | 5, 0 | 8, 0 |

One input at a time it was as p3: no Placeholder but b100t10's one PageDown episode over the narrow report. Only held
PageDown, which outruns the Windows, differs from p3.

## The bytes of a Window's answer

A complete Window, as the scroll's questions were answered (the dense report: as each live run placed its Window, two
answers each). These are medians; JSON is the body as written, and "wire" is its Brotli as sent.

| Report | Read-ahead | Answers | Rows | JSON, KiB | of which metadata | Wire, KiB | Cells filled | Browser: arrived / parse / check, ms | Server: project / write, ms |
|---|---|---|---|---|---|---|---|---|---|
| narrow | b50 | 146 | 100 | 63.3 | 6.6 | 11.9 | 37% | 161 / 19 / 2.0 | 0.3 / 0.5 |
| narrow | b100 | 81 | 200 | 119.7 | 6.6 | 20.6 | 37% | 163 / 35 / 3.4 | 0.5 / 0.8 |
| narrow | b200 | 48 | 400 | 232.5 | 6.6 | 38.3 | 37% | 168 / 68 / 6.5 | 1.0 / 1.4 |
| narrow | 30 | 213 | 75 | 49.1 | 6.6 | 9.8 | 37% | 161 / 16 / 1.6 | 0.2 / 0.4 |
| narrow | 60 | 124 | 135 | 83.0 | 6.6 | 14.9 | 37% | 162 / 25 / 2.5 | 0.4 / 0.5 |
| narrow | 120 | 54 | 255 | 150.6 | 6.6 | 25.4 | 37% | 164 / 44 / 4.6 | 0.8 / 0.9 |
| narrow | b100t10 | 90 | 200 | 119.7 | 6.6 | 20.6 | 37% | 163 / 33 / 3.4 | 0.6 / 0.7 |
| narrow | 60t10 | 148 | 135 | 83.0 | 6.6 | 14.9 | 37% | 161 / 24 / 2.4 | 0.4 / 0.5 |
| wide | b50 | 149 | 100 | 361.4 | 51.8 | 35.5 | 2% | 169 / 44 / 8.1 | 3.3 / 1.3 |
| wide | b100 | 84 | 200 | 668.3 | 51.8 | 63.5 | 2% | 188 / 80 / 16.2 | 6.0 / 2.5 |
| wide | b200 | 48 | 400 | 1,285.6 | 51.8 | 120.1 | 2% | 224 / 137 / 29.9 | 10.1 / 4.7 |
| wide | 30 | 219 | 73 | 276.6 | 51.8 | 27.5 | 2% | 167 / 35 / 6.1 | 2.6 / 1.1 |
| wide | 60 | 126 | 133 | 461.7 | 51.8 | 44.5 | 2% | 174 / 54 / 10.6 | 4.1 / 1.7 |
| wide | 120 | 60 | 253 | 832.2 | 51.8 | 78.5 | 2% | 197 / 96 / 19.9 | 7.0 / 2.9 |
| wide | b100t10 | 90 | 200 | 667.9 | 51.8 | 63.4 | 2% | 187 / 76 / 15.9 | 5.8 / 2.4 |
| wide | 60t10 | 150 | 133 | 461.7 | 51.8 | 44.5 | 2% | 175 / 55 / 10.4 | 4.2 / 1.6 |
| wide-dense | b50 | 2 | 100 | 1,821.9 | 51.8 | 405.4 | 100% | 285 / 332 / 20.8 | 11.6 / 14.2 |
| wide-dense | b100 | 2 | 200 | 3,591.9 | 51.8 | 804.0 | 100% | 461 / 618 / 39.8 | 29.4 / 32.7 |
| wide-dense | b200 | 2 | 200 | 3,591.9 | 51.8 | 804.0 | 100% | 459 / 608 / 38.2 | 21.1 / 21.7 |
| wide-dense | 30 | 2 | 73 | 1,344.0 | 51.8 | 297.9 | 100% | 235 / 254 / 15.9 | 10.2 / 9.3 |
| wide-dense | 60 | 2 | 133 | 2,405.9 | 51.8 | 536.8 | 100% | 305 / 411 / 28.5 | 15.2 / 13.1 |
| wide-dense | 120 | 2 | 223 | 3,998.8 | 51.8 | 895.6 | 100% | 459 / 761 / 50.1 | 25.3 / 26.3 |

- **An answer weighs its rows times its columns, plus the report's metadata**, as the first record found. A block's
  answer weighs what N either side's would at the same rows.
- **Blocks' answers are the largest per question.** b200's 400 rows over the wide report came to 1.3 MiB of JSON (120
  KiB on the wire), parsed in 137 ms. b100's 200 rows over the dense report came to 3.5 MiB (804 KiB on the wire),
  parsed in 618 ms on the browser's one thread.
- **The dense report has 271 rows.** Placed at row 90, b100 and b200 both asked for rows 0–199: two blocks of 100, or
  one of 200. So they hold the same Window there, and N = 120 held rows 0–222.

## What a scroll costs

One input at a time. "Rows a question" and "rows scrolled from one to the next" are steady: ArrowDown's questions one
after another. The per-100-row columns count whole samples.

| Report | Read-ahead | Rows a question | Rows scrolled from one to the next | Rows answered a row scrolled | Questions per 100 rows | JSON per 100 rows, KiB | Wire per 100 rows, KiB | Parse per 100 rows, ms |
|---|---|---|---|---|---|---|---|---|
| narrow | b50 | 100 | 50 | 2.00 | 1.9 | 121.6 | 22.8 | 47 |
| narrow | b100 | 200 | 100 | 2.00 | 1.0 | 116.4 | 20.0 | 42 |
| narrow | b200 | 400 | 200 | 2.00 | 0.6 | 129.3 | 21.3 | 45 |
| narrow | 30 | 75 | 31 | 2.42 | 2.8 | 140.0 | 27.9 | 53 |
| narrow | 60 | 135 | 61 | 2.21 | 1.5 | 121.2 | 21.8 | 48 |
| narrow | 120 | 255 | 121 | 2.11 | 0.6 | 94.3 | 15.9 | 36 |
| narrow | b100t10 | 200 | 100 | 2.00 | 1.0 | 124.8 | 21.5 | 47 |
| narrow | 60t10 | 135 | 51 | 2.65 | 1.8 | 150.0 | 27.0 | 52 |
| wide | b50 | 100 | 50 | 2.00 | 1.9 | 691.5 | 67.7 | 95 |
| wide | b100 | 200 | 100 | 2.00 | 1.0 | 649.3 | 61.7 | 87 |
| wide | b200 | 400 | 200 | 2.00 | 0.6 | 713.1 | 66.6 | 83 |
| wide | 30 | 73 | 31 | 2.35 | 3.0 | 826.5 | 82.5 | 119 |
| wide | 60 | 133 | 61 | 2.18 | 1.5 | 673.1 | 65.0 | 92 |
| wide | 120 | 253 | 121 | 2.09 | 0.7 | 577.2 | 54.5 | 73 |
| wide | b100t10 | 200 | 100 | 2.00 | 1.0 | 695.4 | 66.0 | 89 |
| wide | 60t10 | 133 | 51 | 2.61 | 1.7 | 801.4 | 77.3 | 107 |

- **Steadily, blocks answer two rows for each row scrolled.** Each question asks again for the block the Viewport
  leaves. N either side answers its Window, 2N rows and the Viewport, once every N + 1 rows.
- **The per-100-row columns count where each sample's start fell.** A sample's first Window was asked before it
  began, and it covered from 13 to 185 rows of the 480, by read-ahead and start. So N = 120, whose first Window
  always covered 120, looks cheapest there.
- **Asked early, N = 60's Windows overlap more.** 60t10 asked every 51 rows, for 2.65 rows a row scrolled. b100t10
  asked b100's Windows, 10 rows sooner.

## A live update over the Window held

Ten ticks each, the same ten at every read-ahead; medians, with the range over the ticks in brackets. The Window was
placed at row 1,000 over the narrow and wide reports, and row 90 over the dense one.

| Report, trades a tick | Read-ahead | Window held, rows | Window Changes, rows | Complete instead | JSON, KiB | Wire, KiB | Server: compute / project, ms | Browser: parse / check, ms |
|---|---|---|---|---|---|---|---|---|
| narrow, 1,000 | b50 | 950–1,049 | 7 (6–10) | 3 of 10 | 12.4 (11.7–14.4) | 4.4 (4.1–4.7) | 122 / 1.3 (0.7–1.9) | 11.2 / 5.5 |
| narrow, 1,000 | b100 | 900–1,099 | 14 (13–18) | 3 of 10 | 17.3 (16.7–20.2) | 5.4 (5.3–5.8) | 110 / 2.6 (2.0–3.3) | 14.8 / 5.2 |
| narrow, 1,000 | b200 | 800–1,199 | 26 (20–31) | 3 of 10 | 26.7 (22.3–29.7) | 7.4 (6.6–7.9) | 105 / 4.5 (2.9–6.1) | 17.9 / 10.2 |
| narrow, 1,000 | 30 | 970–1,044 | 5 (5–8) | 3 of 10 | 10.6 (10.6–12.6) | 3.9 (3.9–4.3) | 106 / 1.1 (0.6–1.5) | 10.9 / 4.5 |
| narrow, 1,000 | 60 | 940–1,074 | 9 (8–12) | 3 of 10 | 13.7 (13.0–15.8) | 4.6 (4.5–4.9) | 105 / 1.7 (1.1–1.9) | 13.4 / 5.3 |
| narrow, 1,000 | 120 | 880–1,134 | 18 (16–22) | 3 of 10 | 20.4 (19.3–23.4) | 6.1 (5.8–6.6) | 103 / 2.9 (1.7–6.8) | 14.2 / 6.2 |
| wide, 1,000 | b50 | 950–1,049 | 7 (6–10) | 3 of 10 | 83.7 (80.7–93.4) | 12.4 (12.1–13.4) | 136 / 79.9 (61.2–84.6) | 40.5 / 11.5 |
| wide, 1,000 | b100 | 900–1,099 | 14 (13–18) | 3 of 10 | 110.1 (107.4–123.7) | 16.0 (15.8–17.6) | 152 / 138.5 (111.9–163.3) | 46.9 / 17.8 |
| wide, 1,000 | b200 | 800–1,199 | 26 (20–31) | 3 of 10 | 161.9 (139.5–175.5) | 24.6 (21.3–25.6) | 124 / 254.3 (231.6–278.5) | 44.0 / 31.2 |
| wide, 1,000 | 30 | 970–1,042 | 5 (5–8) | 3 of 10 | 72.7 (72.3–82.3) | 10.2 (10.1–11.1) | 130 / 49.6 (48.3–82.1) | 41.0 / 8.8 |
| wide, 1,000 | 60 | 940–1,072 | 9 (8–12) | 3 of 10 | 90.3 (86.6–100.2) | 13.1 (12.6–14.2) | 120 / 90.1 (72.5–135.8) | 36.2 / 12.2 |
| wide, 1,000 | 120 | 880–1,132 | 18 (16–22) | 3 of 10 | 126.7 (121.7–140.6) | 18.7 (18.2–20.3) | 123 / 168.2 (158.2–191.7) | 41.7 / 21.2 |
| wide-dense, 1,000 | b50 | 50–149 | 98 (97–100) | 0 of 10 | 1,864.1 (1,839.7–1,897.9) | 423.7 (415.2–437.0) | 1,497 / 42.8 (38.7–75.1) | 302.0 / 20.3 |
| wide-dense, 1,000 | b100 | 0–199 | 196 (192–200) | 0 of 10 | 3,644.9 (3,583.0–3,763.8) | 832.5 (809.8–871.9) | 1,466 / 89.2 (81.4–177.0) | 569.5 / 40.0 |
| wide-dense, 1,000 | b200 | 0–199 | 196 (192–200) | 0 of 10 | 3,644.9 (3,583.0–3,763.8) | 831.2 (809.7–871.6) | 1,472 / 97.1 (79.6–146.6) | 559.0 / 39.4 |
| wide-dense, 1,000 | 30 | 60–132 | 72 (70–73) | 0 of 10 | 1,373.6 (1,350.8–1,395.5) | 309.9 (305.2–319.8) | 1,485 / 33.1 (28.2–49.4) | 255.6 / 16.0 |
| wide-dense, 1,000 | 60 | 30–162 | 130 (127–133) | 0 of 10 | 2,453.1 (2,412.2–2,514.3) | 561.5 (546.8–582.6) | 1,467 / 61.1 (53.1–122.5) | 391.7 / 26.5 |
| wide-dense, 1,000 | 120 | 0–222 | 217 (215–222) | 0 of 10 | 4,052.4 (3,981.1–4,191.9) | 925.9 (900.7–972.8) | 1,455 / 102.4 (88.1–192.8) | 611.7 / 42.6 |
| narrow, 20 | b50 | 950–1,049 | 0 | 0 of 10 | 6.9 | 3.0 | 110 / 1.2 (1.1–1.7) | 6.3 / 5.0 |
| narrow, 20 | b100 | 900–1,099 | 0 | 0 of 10 | 6.9 | 3.0 | 120 / 2.7 (2.2–3.5) | 6.6 / 5.9 |
| narrow, 20 | b200 | 800–1,199 | 0 (0–2) | 0 of 10 | 6.9 (6.9–8.5) | 3.0 (3.0–3.6) | 120 / 4.4 (2.4–6.1) | 5.8 / 7.3 |
| narrow, 20 | 30 | 970–1,044 | 0 | 0 of 10 | 6.9 | 3.0 | 117 / 1.0 (0.9–1.7) | 7.8 / 3.5 |
| narrow, 20 | 60 | 940–1,074 | 0 | 0 of 10 | 6.9 | 3.0 | 113 / 1.7 (0.9–2.4) | 7.3 / 6.5 |
| narrow, 20 | 120 | 880–1,134 | 0 | 0 of 10 | 6.9 | 3.0 | 116 / 3.0 (1.6–4.0) | 7.0 / 6.5 |
| wide, 20 | b50 | 950–1,049 | 0 | 0 of 10 | 52.1 | 7.1 | 132 / 71.8 (59.0–86.0) | 23.7 / 12.4 |
| wide, 20 | b100 | 900–1,099 | 0 | 0 of 10 | 52.1 | 7.1 (7.0–7.1) | 137 / 144.0 (118.1–171.8) | 24.2 / 18.2 |
| wide, 20 | b200 | 800–1,199 | 0 (0–2) | 0 of 10 | 52.1 (52.1–62.8) | 7.1 (7.1–9.2) | 125 / 266.3 (225.1–291.2) | 22.1 / 31.5 |
| wide, 20 | 30 | 970–1,042 | 0 | 0 of 10 | 52.1 | 7.1 (7.0–7.1) | 136 / 51.4 (41.7–70.5) | 25.1 / 9.4 |
| wide, 20 | 60 | 940–1,072 | 0 | 0 of 10 | 52.1 | 7.1 | 128 / 90.5 (81.0–120.0) | 24.2 / 11.6 |
| wide, 20 | 120 | 880–1,132 | 0 | 0 of 10 | 52.1 | 7.1 (7.0–7.1) | 133 / 169.6 (148.3–188.2) | 24.9 / 20.1 |
| wide-dense, 20 | b50 | 50–149 | 8 (4–11) | 0 of 10 | 203.8 (123.4–248.7) | 41.5 (23.2–51.7) | 1,506 / 47.8 (40.8–73.3) | 56.8 / 21.8 |
| wide-dense, 20 | b100 | 0–199 | 14 (11–17) | 0 of 10 | 293.4 (248.7–355.9) | 62.0 (51.7–76.3) | 1,422 / 94.2 (77.0–143.6) | 67.8 / 41.1 |
| wide-dense, 20 | b200 | 0–199 | 14 (11–17) | 0 of 10 | 293.4 (248.7–355.9) | 62.0 (51.7–76.3) | 1,439 / 91.9 (83.3–125.6) | 70.0 / 42.4 |
| wide-dense, 20 | 30 | 60–132 | 5 (2–10) | 0 of 10 | 141.3 (87.7–230.6) | 27.3 (15.2–47.7) | 1,441 / 33.0 (30.2–64.7) | 36.9 / 15.4 |
| wide-dense, 20 | 60 | 30–162 | 10 (6–15) | 0 of 10 | 221.7 (159.3–319.7) | 45.6 (31.5–67.7) | 1,427 / 60.6 (54.3–91.9) | 55.2 / 25.6 |
| wide-dense, 20 | 120 | 0–222 | 16 (14–19) | 0 of 10 | 337.5 (301.8–391.1) | 71.7 (63.9–84.1) | 1,451 / 115.8 (89.4–163.9) | 67.8 / 41.4 |

- **Row 1,000 is a block boundary.** With 10 rows either side, every block size held two blocks there: 100, 200 and
  400 rows. A Window placed inside a block holds one, so this is the larger case, the one a scroll leaves it in.
- **The rows a tick changes grow with the Window.** With 1,000-trade ticks over the narrow and wide reports they were
  5, 7, 9, 14, 18 and 26 rows at 30, b50, 60, b100, 120 and b200. Over the dense report nearly the whole Window
  changed.
- **The server projects every row of the Window for every update, changed or not.** Over the wide report that took
  50 to 266 ms by the Window's size, 0.6 to 0.7 ms a row, with no row changed (20-trade ticks) as with some.
- **The 20-trade ticks changed no row of the narrow or wide Windows, at any read-ahead** but b200, whose 400 rows once
  took 2. Their answers were the metadata alone: 6.9 KiB narrow, 52.1 KiB wide.
- **When the row sequence moved, the complete Window came instead**, in 3 of the 10 ticks of 1,000 trades over the
  narrow and wide reports. They are left out of the table's sizes, which are Window Changes only. Those answers were:
  - narrow: 50–51 KiB at 30, 64–66 at b50, 84–87 at 60, 121–125 at b100, 153–158 at 120 and 235–243 at b200;
  - wide: 278–280 KiB at 30, 364–368 at b50, 464–470 at 60, 671–679 at b100, 836–846 at 120 and 1,288–1,304 at b200.

## The Windows the report source keeps (LV-22)

After the ten ticks, the source's own count, and the heap after a full collection with the kept Windows less without
them; three readings each way.

| Report, trades a tick | Read-ahead | Windows / rows | Distinct rows | Value cells | Heap they hold, KiB | Readings within, KiB |
|---|---|---|---|---|---|---|
| narrow, 1,000 | b50 | 2 / 200 | 109 | 654 | 99.8 | 8.0 |
| narrow, 1,000 | b100 | 2 / 400 | 214 | 1,284 | 193.3 | 0.0 |
| narrow, 1,000 | b200 | 2 / 800 | 428 | 2,568 | 382.1 | 0.0 |
| narrow, 1,000 | 30 | 2 / 150 | 81 | 486 | 78.7 | 4.0 |
| narrow, 1,000 | 60 | 2 / 270 | 145 | 870 | 131.7 | 0.0 |
| narrow, 1,000 | 120 | 2 / 510 | 275 | 1,650 | 247.9 | 8.0 |
| wide, 1,000 | b50 | 2 / 200 | 109 | 27,359 | 548.0 | 8.0 |
| wide, 1,000 | b100 | 2 / 400 | 214 | 53,714 | 1,076.3 | 0.0 |
| wide, 1,000 | b200 | 2 / 800 | 428 | 107,428 | 2,145.8 | 0.0 |
| wide, 1,000 | 30 | 2 / 146 | 78 | 19,578 | 393.2 | 8.0 |
| wide, 1,000 | 60 | 2 / 266 | 143 | 35,893 | 720.8 | 0.0 |
| wide, 1,000 | 120 | 2 / 506 | 273 | 68,523 | 1,373.9 | 0.0 |
| wide-dense, 1,000 | b50 | 2 / 200 | 198 | 49,698 | 5,839.7 | 0.0 |
| wide-dense, 1,000 | b100 | 2 / 400 | 392 | 98,392 | 11,560.1 | 0.0 |
| wide-dense, 1,000 | b200 | 2 / 400 | 392 | 98,392 | 11,560.1 | 0.0 |
| wide-dense, 1,000 | 30 | 2 / 146 | 144 | 36,144 | 4,247.9 | 0.0 |
| wide-dense, 1,000 | 60 | 2 / 266 | 260 | 65,260 | 7,667.6 | 0.0 |
| wide-dense, 1,000 | 120 | 2 / 446 | 438 | 109,938 | 12,915.6 | 0.0 |
| narrow, 20 | b50 | 2 / 200 | 100 | 600 | 92.5 | 8.0 |
| narrow, 20 | b100 | 2 / 400 | 200 | 1,200 | 181.5 | 0.0 |
| narrow, 20 | b200 | 2 / 800 | 400 | 2,400 | 357.4 | 8.0 |
| narrow, 20 | 30 | 2 / 150 | 75 | 450 | 69.3 | 0.0 |
| narrow, 20 | 60 | 2 / 270 | 135 | 810 | 124.3 | 0.0 |
| narrow, 20 | 120 | 2 / 510 | 255 | 1,530 | 230.7 | 0.0 |
| wide, 20 | b50 | 2 / 200 | 100 | 25,100 | 500.4 | 0.0 |
| wide, 20 | b100 | 2 / 400 | 200 | 50,200 | 990.0 | 0.0 |
| wide, 20 | b200 | 2 / 800 | 400 | 100,400 | 1,973.1 | 0.0 |
| wide, 20 | 30 | 2 / 146 | 73 | 18,323 | 362.3 | 0.0 |
| wide, 20 | 60 | 2 / 266 | 133 | 33,383 | 660.9 | 0.0 |
| wide, 20 | 120 | 2 / 506 | 253 | 63,503 | 1,252.6 | 8.0 |
| wide-dense, 20 | b50 | 2 / 200 | 105 | 26,355 | 3,128.8 | 8.0 |
| wide-dense, 20 | b100 | 2 / 400 | 212 | 53,212 | 6,313.3 | 0.0 |
| wide-dense, 20 | b200 | 2 / 400 | 212 | 53,212 | 6,313.3 | 0.0 |
| wide-dense, 20 | 30 | 2 / 146 | 75 | 18,825 | 2,236.3 | 0.0 |
| wide-dense, 20 | 60 | 2 / 266 | 139 | 34,889 | 4,140.3 | 0.0 |
| wide-dense, 20 | 120 | 2 / 446 | 237 | 59,487 | 7,563.0 | 506.6 |

- **The source keeps two Windows once a live update has come**, one per Report Version kept, each read ahead, as the
  first record found. What they hold grows with their rows, whatever the Window's shape.
- **A kept row holds what the first record found**: about 0.9 KiB narrow, 5 KiB wide and 29.5 KiB dense.
- **One reading is out of line**: the dense report, 20-trade ticks, N = 120, whose three readings were 507 KiB apart.
  Its figure is as good as that.

## What these numbers do not show

- **ag-grid itself.** No ag-grid ran here. This is ExPivot asking as ag-grid's rule asks, over one contiguous Window.
  - ag-grid keeps every block. Here a block the Viewport has left is asked for again inside the next Window. Only
    scrolling down was measured, so what scrolling back costs is not in these tables.
  - ag-grid asks for up to two blocks at once (`maxConcurrentDatasourceRequests`). ExPivot has one question out at a
    time.
  - With every block kept, ag-grid's question is one block, 100 rows. Here, scrolling on, it is two blocks, 200 rows,
    half of them rows already held. A block cache would halve what blocks carry. It would not shrink what a live
    update projects, which is every row held.
- **What aligned blocks would let a server do.** An aligned Window is the same Window from every row of its blocks,
  so a server or an HTTP cache could answer it again from what it answered before. Nothing here caches answers, and N
  rows either side asks a different Window from almost every row.
- **What the trigger costs when a scroll stops.** Asked 10 rows early, a question goes out for rows that a scroll
  stopping within those 10 rows never shows. The tables count every question asked, but the samples never stop short.
- **A real network**, as the first record says. The loopback carries any number of bytes in no time, so the larger
  answers of blocks cost here only their reading. Over 10 Mbit/s, b200's wide answer of 120 KiB of Brotli would take
  about 0.1 s more, and b100's dense one of 804 KiB about 0.66 s more.
- **Real input, other browsers and platforms, Blazor Server, other heights and layouts, many users, live updates at
  the demo's pace, scrolling up, flings and scrolling sideways**: as the first record says, none of them.
- **Jumps.** A Window placed by a jump holds as little as 10 rows below the Viewport under blocks, and always N rows
  under N either side. The three samples start at three such places, so the tables hold both, but a run of jumps was
  not measured.

## Reproducing

On `lab/remote-read-ahead` at `21d7a94`, with nothing else running on the machine, from `tests/ExGrid.Browser`. Each
pass is one run of `read-ahead-run.sh`, with its own output directory:

```sh
# Publishes the hosts in Release (once), then the blocks of 100 and N = 60, scrolling and live updates (about 19 min).
EXGRID_PUBLISH=1 EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw>/p1 \
  EXGRID_MEASURE_READ_AHEAD=b100,60 EXGRID_MEASURE_LIVE_READ_AHEAD=b100,60 bash read-ahead-run.sh
EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw>/p2 \
  EXGRID_MEASURE_READ_AHEAD=b50,b200 EXGRID_MEASURE_LIVE_READ_AHEAD=b50,b200 bash read-ahead-run.sh
EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw>/p3 \
  EXGRID_MEASURE_READ_AHEAD=b100t10,60t10 EXGRID_MEASURE_PARTS=scroll bash read-ahead-run.sh
EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw>/p4 \
  EXGRID_MEASURE_READ_AHEAD=30,120 EXGRID_MEASURE_LIVE_READ_AHEAD=30,120 bash read-ahead-run.sh
# The dense report over a million trades: live updates only (about 7 min).
EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw>/p5 EXGRID_DEMO_TRADES=1000000 EXGRID_MEASURE_LAYOUTS=wide-dense \
  EXGRID_MEASURE_PARTS=live EXGRID_MEASURE_LIVE_READ_AHEAD=b50,b100,b200,30,60,120 bash read-ahead-run.sh
```

- **p1 and p2 ran at `a657b79`**, whose blocks and N either side ask as `21d7a94`'s do.
- **The ports**: the page's is 5392 (`EXGRID_PORT`), the proxy listens at 8392, and the demo API at 9392.
- **A local Playwright config** (`EXGRID_CONFIG`) launched Playwright's own Chromium. The committed config launches the
  installed Chrome.
- **A git worktree**, whose flake nix cannot read as a path, takes `EXGRID_FLAKE=git+file:///<worktree>?shallow=1`.
- **The tables**: once the per-sample files are gzipped into `raw/` as here, `python3 tables.py` prints them.
