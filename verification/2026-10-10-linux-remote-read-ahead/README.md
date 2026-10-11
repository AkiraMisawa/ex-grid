# A remote report's Window read ahead: what it would buy, measured (ticket 14)

Date: 2026-10-10, from 23:06 UTC, to 00:20 UTC on 2026-10-11. `lab/remote-read-ahead` at `f4ec680`, a lab
branch that is never merged, built on `claude/backlog-cleanup` at `eda5efa`. This is the measurement
[ticket 14](../../docs/specs/live-data/issues/14-a-remote-reports-window-is-read-ahead.md) asks for before
anything is decided. Over a report source served on a network, it asks what reading ahead would buy ExPivot:
- how long Placeholders stay on screen while the report is scrolled by the wheel, by ArrowDown and by PageDown;
- what a Window's answer weighs, for a narrow report and for one of hundreds of value columns;
- what a live update over the wider Window carries, and what it costs the server to project;
- which Windows the report source keeps (LV-22).

All of it at 0, 30, 60 and 120 rows read ahead.

It decides nothing. Performance never gates (AGENTS.md), and every time here is observational. Counts and
bytes are exact. The prototype and the harness are on the lab branch. The raw files are in [`raw/`](raw/):
- the per-sample files are gzipped JSON, read with `zcat`;
- the `*-summary.json` files are the run's own summaries.

[`tables.py`](tables.py) prints every table below from the per-sample files.

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Ubuntu 24.04.5, Linux 6.18; .NET
  SDK 10.0.203 through nix, runtime 10.0.7; Node 24.14.1, Playwright 1.62.1. Nothing else ran on it meanwhile.
- **The browser was Playwright's bundled Chromium 141.0.7390.37, headed under xvfb (2,560 × 1,600 × 24) on this
  Linux container, not branded Chrome or Edge**, which the container does not have. A local Playwright
  config, never committed, launched it in place of the channels. The page was 1,400 × 1,100. The console stayed
  clean in every test (CON-1 to CON-3).
- **The code**: `f4ec680`, whose `src/` and `samples/` are those of `2be7ffb`; `f4ec680` changed only the
  harness, after two short trial runs (below). The hosts were published from `f4ec680` in Release, as CI's
  hosts job publishes them:
  - the DemoHost as WebAssembly, without AOT and without the `wasm-tools` workload, served as static files by
    `static-host.mjs`;
  - the demo API server under `ASPNETCORE_ENVIRONMENT=Development`, holding 20,000 trades (layer 3's count):
    270 trade dates, 194 books, 49 quantities and 5 products. A second pass held 1,000,000.
  - The server compresses its answers as the browser asks, with Brotli at its fastest level (`DemoCompression`).
- **The round trip**: `tests/ExGrid.Browser/latency-proxy.mjs` stood between the browser and the demo API server
  and held every chunk 75 ms each way: 150 ms added to each round trip, as ADR-0151's experiment added it. The
  page's own files were served directly. The proxy delays bytes, not connection setup, and the loopback has no
  bandwidth limit. The lab answers CORS preflights for two hours, so each question cost the one round trip
  under measurement.
- **The prototype**: `ExPivot.ReadAheadRows`, N. When the grid raises a Range Request, ExPivot asks its report
  source for the range widened by N rows either side, clamped into the report. Two rules govern what it asks:
  - A range that the Window last asked for covers asks nothing while that question is out or its answer is
    held. Any other range supersedes the question in flight, as every Window question does today. N = 0 asks
    exactly what ExPivot asks without the switch.
  - **60s** is the comparison: 60 rows, with any other Window superseding a question in flight even when it
    covers the range. This is `GridSource.Fetch`'s rule (ADR-0025).
- **The page**: `/lab-read-ahead`, one ExPivot over the demo API's report source (`PivotReportSource.Fetch`, as
  `/pivot-db`'s server pivot), with the Field List hidden, at ExPivot's default height of 400 px. Three layouts,
  each with Sum of P&L as its value:
  - **narrow**: Rows Trade date and Book, Columns Product. It has 16,823 rows, 1.2 trades a row and 6 value
    columns. 15 rows are painted, 13 of them whole, so PageDown moves 13 rows.
  - **wide**: the same rows, Columns Product and Quantity: 16,823 rows and 251 value columns, of which a row
    fills about 2%. 13 rows are painted, since its horizontal scrollbar takes a strip; PageDown moves 11.
  - **wide-dense**: Rows Trade date alone, the same columns, over the server holding 1,000,000 trades: 271 rows,
    every cell filled. This is too short to scroll 480 rows, so only its Window's bytes and its live updates
    are measured.
- **Scrolling** (`measure-read-ahead.spec.mjs`).
  - **Samples.** Each sample scrolls 480 rows down, starting from row 200, 8,172 or 16,143: three samples a
    case, the same starts at every N.
  - **Inputs.** The wheel moves 100 px a notch. ArrowDown is pressed with the Focus at the Viewport's foot,
    so that each press scrolls a row. PageDown is pressed from the top row.
  - **Paces.** Each input runs one at a time (each left to settle) and held (paced by the page's animation
    frames). The pace achieved is read back from the inputs' own time stamps; over both reports:
    - the wheel, about 18 notches a second (a median 56 ms apart, 51–68);
    - ArrowDown, about 24 presses a second (41 ms, 34–64);
    - PageDown, about 21 presses a second (49 ms, 35–66).
    
    Held inputs went slower as N grew: ArrowDown's were 35 ms apart at 0 and 54 ms at 120. A larger answer
    kept the browser's one thread busy longer, so a held scroll at 120 ran that much slower than one at 0.
  - **What is timed.** Rows not held on screen. A MutationObserver installed before the app's first script
    times them on the page's own clock: from the animation frame after the DOM first shows a row with no
    cell (a Placeholder for rows not held) to the frame after it shows none.
    - Placeholders of a fling, which paint the Pinned Columns, are counted apart. None appeared.
  - **Waits.** A wait waits for what it reads (ADR-0056): the input's effect, then a settled grid. Settled
    means the scroll still for two frames, the painted slice the one the offset shows, no Placeholder, and
    no question out.
  - **Warm-up.** Every page load is warmed with a few settled inputs before it is timed.
- **Answers**: every Window question as the network saw it. For each it records:
  - its JSON bytes, split into the metadata's and the rows' by the body's own spans;
  - its bytes on the wire (Brotli, as the browser asks);
  - the question's bytes.
  
  The server's times come from the lab's `Server-Timing` header, the browser's from the page: the answer's
  arrival, its parse, and the client's check with the Window Digest.
- **Live updates**: each run starts from the generated trades again (`/api/reset`). The page is loaded and its
  Window placed at row 1,000 (row 90 for wide-dense) with N either side. Then come ten ticks of the server's
  live updates, each answered before the next. The ticks use seeds 1 to 10, so every N takes the same
  changes.
  - Ticks move 20 trades (the demo's) or 1,000 (PV-21's batch).
  - Each changes P&L, one in four a notional as well, and one tick in eight cancels a trade and books another.
  - ExPivot is told of each tick and asks again for the Window it holds, with its Baseline Window.
  - The source declares full refresh, so the server computes the report again in SQL, projects every row of
    the Window, and answers with Window Changes.
- **The Windows kept**: the report source's own count after the ticks. Then the heap after a full collection,
  three readings with the kept Windows and three without, once they are let go.
- **Trials**: two short trial runs came first, at `2be7ffb`. They changed three things before the run:
  - Held inputs had gone slower than meant (keys 54 ms apart), so the held pace was raised.
  - A page's first questions ran cold, so the warm-ups were added.
  - A 20-trade tick changed no row of a Window 16,823 rows deep, so the 1,000-trade ticks were added.

## In brief

- **Without read-ahead, every row a scroll brings in shows a Placeholder for a round trip.**
  - One episode lasted a median 164–184 ms, 150 ms of it the added round trip.
  - Pressed one at a time, ArrowDown showed one 480 times in 480 rows.
  - Held, each input superseded the question before it (479 of 480 for ArrowDown), so no answer landed while
    the scroll lasted: Placeholders were on screen 96–100% of the scroll's time.
- **Read ahead, an episode is no shorter, and it is rarer.**
  - ArrowDown, one at a time, met one every N + 1 rows: 15, 7 and 3 in 480 rows at 30, 60 and 120.
  - Held wheel and ArrowDown showed Placeholders for 36–38% and 16–19% of the scroll's time at 30, 19–21% and
    7–8% at 60, and 8–11% and 3–4% at 120.
  - Held PageDown, 11–13 rows a press at about 21 a second, outran the Window. Its rows were not held for 90–92%
    of the time at 30, 63–72% at 60 and 27–36% at 120.
  - An episode grew with N, since its answer took longer to read: ArrowDown's from 164 to 206 ms over the
    narrow report, and from 178 to 291 ms over the wide one.
- **Keeping the covering question in flight is what made held scrolling work.** Under 60s, each input still
  superseded the question before it. Held wheel and ArrowDown showed Placeholders 88% of the time, against
  19–21% and 7–8%, and held PageDown 85–86% against 63–72%. One input at a time, the two are the same, since
  nothing is out to supersede.
- **An answer grows with its rows times its columns, and every answer carries the report's metadata.**
  - **narrow**, from 15 to 255 rows: 15.3 to 150.6 KiB of JSON (4.4 to 25.4 KiB on the wire), parsed in the
    browser in 5 to 43 ms.
  - **wide, sparse**, from 13 to 253 rows: 91.3 to 832 KiB (10.4 to 78.5), 51.8 KiB of it metadata, parsed in
    16 to 93 ms.
  - **wide-dense**, from 13 to 223 rows: 282 KiB to 3.9 MiB (59 to 896 KiB), parsed in 76 to 675 ms on the
    browser's one thread.
  - Every question carried 5.7 KiB of JSON whatever N was: its settings, the label metrics among them.
- **A live update's Window Changes grow with the Window, and the server projects every row of it.**
  - With 1,000-trade ticks, the changes carried 0, 5, 9 and 18 rows at 0, 30, 60 and 120 (narrow and wide), and
    13, 72, 130 and 217 over the dense report: 288 KiB to 4.0 MiB, parsed in 58 to 612 ms.
  - With 20-trade ticks, no row of the narrow or sparse wide Windows changed. Each answer was the report's
    metadata alone: 6.9 KiB narrow, 52.1 KiB wide.
  - The server's projection of the Window took 0.3 to 3.3 ms narrow, 9 to 175 ms wide and 6 to 104 ms dense, even
    when no row changed. This is beside 105–187 ms of computing the report again in SQL over 20,000 trades, and
    1.4 s over a million.
- **The report source kept two Windows after the ticks (LV-22), one per Report Version it keeps, each read ahead.**
  The heap they held was about 0.9 KiB a distinct row narrow, 5 KiB wide and 29.5 KiB dense: up to 0.24, 1.3 and
  12.6 MiB a report at 120.

## Placeholders while scrolling

The time rows not held were on screen, per 480 rows: the median of three samples, and in brackets the
episodes and one episode's median. "Held" gives the share of the scroll's time instead.

### narrow (6 value columns)

| Input | 0 | 30 | 60 | 120 | 60s |
|---|---|---|---|---|---|
| Wheel, a notch at a time | 22.6 s (134 × 167 ms) | 2.6 s (14 × 182) | 1.4 s (7 × 204) | 0.73 s (3 × 249) | 1.4 s (7 × 205) |
| ArrowDown, a press at a time | 80.2 s (480 × 164) | 2.7 s (15 × 174) | 1.3 s (7 × 185) | 0.62 s (3 × 206) | 1.4 s (7 × 187) |
| PageDown, a press at a time | 6.1 s (37 × 164) | 2.1 s (12 × 174) | 1.3 s (7 × 184) | 0.64 s (3 × 208) | 1.3 s (7 × 186) |
| Wheel, held | 99% | 36% | 19% | 8% | 88% |
| ArrowDown, held | 100% | 16% | 7% | 3% | 88% |
| PageDown, held | 96% | 90% | 72% | 27% | 85% |

### wide (251 value columns)

| Input | 0 | 30 | 60 | 120 | 60s |
|---|---|---|---|---|---|
| Wheel, a notch at a time | 25.0 s (134 × 184 ms) | 3.1 s (14 × 225) | 1.9 s (7 × 260) | 1.1 s (3 × 371) | 1.8 s (7 × 264) |
| ArrowDown, a press at a time | 86.7 s (480 × 178) | 3.2 s (15 × 208) | 1.7 s (7 × 241) | 0.88 s (3 × 291) | 1.8 s (7 × 244) |
| PageDown, a press at a time | 8.0 s (44 × 181) | 3.0 s (14 × 207) | 1.7 s (7 × 241) | 1.2 s (4 × 296) | 1.7 s (7 × 239) |
| Wheel, held | 99% | 38% | 21% | 11% | 88% |
| ArrowDown, held | 100% | 19% | 8% | 4% | 88% |
| PageDown, held | 97% | 92% | 63% | 36% | 86% |

Questions asked while held, and superseded:

| Report, input | 0 | 30 | 60 | 120 | 60s |
|---|---|---|---|---|---|
| narrow, wheel | 134, 133 | 14, 0 | 7, 0 | 3, 0 | 117, 116 |
| narrow, ArrowDown | 480, 479 | 15, 0 | 7, 0 | 3, 0 | 420, 419 |
| narrow, PageDown | 37, 36 | 12, 11 | 7, 1 | 3, 0 | 33, 32 |
| wide, wheel | 134, 133 | 14, 0 | 7, 0 | 3, 0 | 117, 116 |
| wide, ArrowDown | 480, 479 | 15, 0 | 7, 0 | 3, 0 | 420, 419 |
| wide, PageDown | 44, 43 | 14, 13 | 7, 0 | 4, 0 | 39, 38 |

- **Each sample started on the Window asked when its start was placed**: 15, 75, 135 and 255 rows narrow, 13, 73,
  133 and 253 wide. That is the rows painted plus N either side. A scroll inside it asked nothing: 480 rows at
  N = 120 asked 3 or 4 times.
- **An episode is one round trip and the reading of its answer.** It is no shorter at any N. Read ahead, it comes
  once the scroll has passed the rows read ahead, so ArrowDown met one every N + 1 rows.
- **Held, an input that arrives while the question is out**:
  - Under the prototype it asks nothing while that question's Window covers the range, so the answer lands.
  - Under 60s, and at N = 0, it supersedes the question, and nothing lands until the inputs stop. 60s kept
    only the first 60 rows' advantage: 420 questions in 480 rows, against 480 at N = 0.
  - Held PageDown moved about 300 rows a second at 0 to 60, so 60 rows read ahead lasted it about 0.2 s, about
    one episode. At 30 it ran past the Window asked within the round trip and superseded it, 11 and 13 times.
- Every failure the network saw was a superseded question, aborted (`net::ERR_ABORTED`): exactly the page's own
  count. No question failed otherwise.

## The bytes of a Window's answer

A complete Window, as the scroll's questions were answered (wide-dense: as each live run placed its Window,
two answers each). These are medians; JSON is the body as written, and "wire" is its Brotli as sent.

| Report | N | Rows | JSON, KiB | of which metadata | of which rows | Wire, KiB | Cells filled | Browser: arrived / parse / check, ms | Server: project / write, ms |
|---|---|---|---|---|---|---|---|---|---|
| narrow | 0 | 15 | 15.3 | 6.6 | 8.4 | 4.4 | 37% | 158 / 5 / 0.6 | 0.1 / 0.2 |
| narrow | 30 | 75 | 49.1 | 6.6 | 42.3 | 9.8 | 37% | 160 / 15 / 1.5 | 0.2 / 0.4 |
| narrow | 60 | 135 | 83.0 | 6.6 | 76.2 | 14.9 | 37% | 161 / 25 / 2.4 | 0.4 / 0.5 |
| narrow | 120 | 255 | 150.6 | 6.6 | 143.8 | 25.4 | 37% | 163 / 43 / 4.1 | 0.7 / 0.9 |
| wide | 0 | 13 | 91.3 | 51.8 | 39.3 | 10.4 | 1% | 160 / 16 / 1.5 | 0.6 / 0.5 |
| wide | 30 | 73 | 276.6 | 51.8 | 224.6 | 27.5 | 2% | 167 / 36 / 6.0 | 2.5 / 1.1 |
| wide | 60 | 133 | 461.7 | 51.8 | 409.8 | 44.5 | 2% | 172 / 57 / 10.6 | 4.0 / 1.6 |
| wide | 120 | 253 | 832.2 | 51.8 | 780.2 | 78.5 | 2% | 195 / 93 / 19.8 | 6.8 / 2.9 |
| wide-dense | 0 | 13 | 282.1 | 51.8 | 230.2 | 58.8 | 100% | 197 / 76 / 4.1 | 1.8 / 1.7 |
| wide-dense | 30 | 73 | 1,344.0 | 51.8 | 1,292.1 | 297.9 | 100% | 284 / 240 / 19.8 | 8.7 / 7.5 |
| wide-dense | 60 | 133 | 2,405.9 | 51.8 | 2,353.9 | 536.8 | 100% | 379 / 411 / 27.9 | 14.8 / 21.9 |
| wide-dense | 120 | 223 | 3,998.8 | 51.8 | 3,946.8 | 895.6 | 100% | 487 / 675 / 43.0 | 24.2 / 22.4 |

- **A row costs its columns, whether they hold values or not.** Every row carries every column: its values and
  their change marks, `null` where empty.
  - A narrow row is 0.6 KiB of JSON, a sparse wide row 3.1 KiB, and a dense wide row 17.7 KiB.
  - The browser parsed 0.17, 0.37 and 3.0 ms a row of them.
  - The dense report's 223 rows at N = 120 (clamped at row 0) kept the browser's one thread busy for 0.68 s
    reading the answer and 43 ms checking it.
- **The metadata comes with every answer**: 6.6 KiB narrow, 51.8 KiB wide, every column's definition included.
  That is 57% of a wide answer at N = 0, and all of a Window Changes answer that changes nothing.
- **"Arrived" is the time from asking to the whole body read in the browser**: the 150 ms round trip, the server,
  and on the loopback the bytes, which cost no time on the wire here. The dense answer's 3.9 MiB took 487 ms.
- **Every question was 5.7 KiB of JSON (5.8 wide)** at every N: its settings, the label metrics' glyph widths among
  them.

## A live update over the wider Window

Ten ticks each, the same ten at every N; medians, with the range over the ticks in brackets.

| Report, trades a tick | N | Window rows | Window Changes, rows | JSON, KiB | Wire, KiB | Server: compute / project, ms | Browser: parse / check, ms |
|---|---|---|---|---|---|---|---|
| narrow, 20 | 0 | 15 | 0 | 6.9 | 3.0 | 125 / 0.3 | 7.4 / 1.7 |
| narrow, 20 | 30 | 75 | 0 | 6.9 | 3.0 | 114 / 1.1 | 6.8 / 3.8 |
| narrow, 20 | 60 | 135 | 0 | 6.9 | 3.0 | 111 / 1.7 | 5.7 / 6.7 |
| narrow, 20 | 120 | 255 | 0 | 6.9 | 3.0 | 108 / 3.3 | 7.5 / 5.8 |
| narrow, 1,000 | 0 | 15 | 0 (0–1) | 6.9 | 3.0 | 105 / 0.3 | 9.1 / 1.5 |
| narrow, 1,000 | 30 | 75 | 5 (5–8) | 10.6 | 3.9 | 114 / 1.0 | 10.1 / 4.4 |
| narrow, 1,000 | 60 | 135 | 9 (8–12) | 13.7 | 4.6 | 107 / 1.7 | 15.3 / 4.7 |
| narrow, 1,000 | 120 | 255 | 18 (16–22) | 20.4 | 6.1 | 105 / 3.0 | 15.2 / 6.6 |
| wide, 20 | 0 | 13 | 0 | 52.1 | 7.1 | 137 / 9.1 | 27.6 / 2.6 |
| wide, 20 | 30 | 73 | 0 | 52.1 | 7.1 | 187 / 52.6 | 27.4 / 12.2 |
| wide, 20 | 60 | 133 | 0 | 52.1 | 7.1 | 134 / 89.2 | 25.9 / 15.2 |
| wide, 20 | 120 | 253 | 0 | 52.1 | 7.1 | 146 / 174.6 | 25.0 / 20.6 |
| wide, 1,000 | 0 | 13 | 0 (0–1) | 52.1 | 7.1 | 127 / 9.2 | 27.5 / 2.2 |
| wide, 1,000 | 30 | 73 | 5 (5–8) | 72.7 | 10.2 | 149 / 50.5 | 30.2 / 9.2 |
| wide, 1,000 | 60 | 133 | 9 (8–12) | 90.3 | 13.1 | 142 / 93.7 | 37.3 / 13.1 |
| wide, 1,000 | 120 | 253 | 18 (16–22) | 126.7 | 18.7 | 142 / 166.8 | 46.2 / 22.1 |
| wide-dense, 20 | 0 | 13 | 0 (0–1) | 61.0 | 9.1 | 1,439 / 6.0 | 24.8 / 4.5 |
| wide-dense, 20 | 30 | 73 | 5 (2–10) | 141.3 | 27.3 | 1,416 / 35.7 | 32.1 / 14.8 |
| wide-dense, 20 | 60 | 133 | 10 (6–15) | 221.7 | 45.6 | 1,407 / 57.2 | 50.1 / 26.5 |
| wide-dense, 20 | 120 | 223 | 16 (14–19) | 337.5 | 71.7 | 1,435 / 104.1 | 75.9 / 42.5 |
| wide-dense, 1,000 | 0 | 13 | 13 (12–13) | 287.8 | 60.9 | 1,427 / 5.9 | 58.2 / 4.0 |
| wide-dense, 1,000 | 30 | 73 | 72 (70–73) | 1,373.6 | 309.8 | 1,453 / 31.5 | 224.3 / 15.3 |
| wide-dense, 1,000 | 60 | 133 | 130 (127–133) | 2,453.1 | 561.0 | 1,424 / 58.2 | 384.2 / 26.5 |
| wide-dense, 1,000 | 120 | 223 | 217 (215–222) | 4,052.4 | 925.8 | 1,402 / 100.3 | 611.9 / 42.4 |

- **The rows a tick changes in the Window grow with the Window, in proportion.**
  - A long, sparse report (narrow and wide share their 16,823 rows and their ticks) took 0, 5, 9 and 18 rows of
    the 1,000-trade ticks.
  - The demo's 20-trade ticks never reached its Window, at any N. On a report like it, most live updates carry
    no row, only the metadata.
  - A dense report's Window is changed almost whole by 1,000 trades, and by 20 trades as many rows as there are
    changes.
- **When the row sequence moved, the whole Window came instead of Window Changes.** A booked or cancelled trade
  adds or removes a row, and this happened in 3 of the 10 ticks of 1,000 trades, at every N. Those answers were
  the complete Window: 15–16 KiB narrow at 0, rising to 153–158 KiB at 120, and 91–92 KiB wide at 0, rising to
  836–846 KiB.
  These are left out of the table's sizes, which are Window Changes only.
- **The server projects every row of the Window for every update**, changed or not, and compares it with the
  Baseline Window's row.
  - Over 6 columns it took 0.3 to 3.3 ms. Over 251 it took 9 to 175 ms, 0.7 ms a row: more than the report's
    computation over 20,000 trades at N = 120 (about 140 ms).
  - This happened with no row changing (20-trade ticks).
  - A projected row looks up each value column among the previous Window's by name, so its cost grows faster
    than its columns do.
  - A scroll's complete Window, with no Baseline Window to compare, took 6.8 ms for 253 wide rows.
- **The client checks the Window Digest over the whole Window after every update**: 1.5 to 6.7 ms narrow, 2.2 to
  22 ms wide, 4 to 42 ms dense, on the browser's one thread.

## The Windows the report source keeps (LV-22)

After the ten ticks, the source's own count, and the heap after a full collection with the kept Windows less
without them. It took three readings each way; the readings agreed within 8.4 KiB.

| Report, trades a tick | N | Windows / rows | Distinct rows | Value cells | Heap they held, KiB |
|---|---|---|---|---|---|
| narrow, 20 | 0 / 30 / 60 / 120 | 2 / 30, 150, 270, 510 | 15, 75, 135, 255 | 90 – 1,530 | 14, 69, **645**, 231 |
| narrow, 1,000 | 0 / 30 / 60 / 120 | 2 / 30, 150, 270, 510 | 15, 81, 145, 275 | 90 – 1,650 | 7, 75, 132, 248 |
| wide, 20 | 0 / 30 / 60 / 120 | 2 / 26, 146, 266, 506 | 13, 73, 133, 253 | 3,263 – 63,503 | 63, 362, 661, 1,253 |
| wide, 1,000 | 0 / 30 / 60 / 120 | 2 / 26, 146, 266, 506 | 13, 78, 143, 273 | 3,263 – 68,523 | 56, 393, 721, 1,374 |
| wide-dense, 20 | 0 / 30 / 60 / 120 | 2 / 26, 146, 266, 446 | 13, 75, 139, 237 | 3,263 – 59,487 | 389, 2,236, 4,140, 7,057 |
| wide-dense, 1,000 | 0 / 30 / 60 / 120 | 2 / 26, 146, 266, 446 | 26, 144, 260, 438 | 6,526 – 109,938 | 768, 4,248, 7,668, 12,916 |

- **The source keeps two Windows once a live update has come, and one while only scrolling.** One is kept per
  Report Version the source keeps (`versionsKept`, two), each the Baseline Window of the next Window Changes and
  each read ahead.
  - A row unchanged between the two versions is one instance. The Windows' rows are twice the Window, and their
    distinct rows the Window plus the rows that changed.
  - While scrolling, the source answered a moved Window under the same Report Version and kept one: every
    scroll answer's own measure says 1.
- **What a kept row holds depends on its columns and how full they are**: about 0.9 KiB narrow, 5 KiB wide and
  29.5 KiB dense.
- **One reading is out of line**, in bold: narrow, 20-trade ticks, N = 60.
  - Its readings agreed within 8 KiB each way, but the heap fell by 645 KiB where 135 rows hold about 130 KiB
    elsewhere.
  - Something else in the server let go of memory between the two sets of readings. The store's gate held its
    report questions, nothing else.
  - The counts are exact; the heap figures are as good as that.

## What these numbers do not show

- **A real network.**
  - The loopback carries any number of bytes in no time. Over a link of 10 Mbit/s a dense answer of 896 KiB
    would take about 0.7 s more, and a narrow one of 25 KiB 20 ms.
  - The proxy delays bytes, not connections. A superseded question is aborted, and the browser opens another
    connection for the next, which a real network charges a round trip for (and TLS more). That cost falls on
    N = 0 and 60s, the cases that supersede.
  - There was no jitter or loss, and only HTTP/1.1.
- **Real input.** The inputs were synthesized through the browser's protocol at a fixed pace. A touchpad's
  continuous scroll, a wheel with a coarser notch, and other key-repeat rates are other paces. Only held
  PageDown, the fastest, shows what a faster pace does to a given read-ahead.
- **Branded Chrome or Edge, Windows or macOS, a GPU.** This was Playwright's Chromium under xvfb on one Linux
  container, with three samples a case.
- **Blazor Server.** There ExPivot and its report source share the server, and no round trip is read ahead of.
- **Other heights and layouts.** Only ExPivot's default 400 px, and these three layouts, were measured. Report
  filters, many value fields and percentages were not.
- **Many users at once.** One report was asked at a time. The demo's store answers every report's questions under
  one gate, and keeps up to eight reports. What eight wide-dense reports at 120 would hold, about 100 MiB of
  kept Windows, is that arithmetic, not a measurement.
- **Live updates at the demo's pace.** The demo runs four ticks a second; here each answer landed before the next
  tick. The browser's memory for the wider Window was not measured either.
- **Scrolling up, a fling, horizontal scrolling.** The read-ahead is the same either side. A fling asks only once
  it settles (ADR-0004). Every column is in the Window, so scrolling sideways asks nothing.
- **Asking before the Window runs out.** The grid raises a Range Request only for rows it does not hold (ADR-0001).
  So every read-ahead here still paid one round trip of Placeholders each time a scroll passed it. A question
  asked ahead of that edge was not built.

## Reproducing

On `lab/remote-read-ahead` at `f4ec680`, with nothing else running on the machine, from `tests/ExGrid.Browser`:

```sh
# Publishes the hosts in Release, then the narrow and wide reports, scrolling and live updates (about 65 min).
EXGRID_PUBLISH=1 EXGRID_HOSTS=<hosts> EXGRID_MEASURE_OUT=<raw> bash read-ahead-run.sh
# The dense report over a million trades: live updates only (about 5 min, the first time generating them).
EXGRID_HOSTS=<hosts> EXGRID_DEMO_TRADES=1000000 EXGRID_MEASURE_LAYOUTS=wide-dense EXGRID_MEASURE_PARTS=live \
  EXGRID_MEASURE_OUT=<raw> bash read-ahead-run.sh
```

- **The ports**: the page's is 5392 (`EXGRID_PORT`), the proxy listens at 8392, and the demo API at 9392.
- **A local Playwright config**: this run used one (`EXGRID_CONFIG`) to launch Playwright's own Chromium. The
  committed config launches the installed Chrome.
- **A git worktree**, whose flake nix cannot read, takes another checkout's flake in `EXGRID_FLAKE`.
- **The tables**: once the per-sample files are gzipped into `raw/` as here, `python3 tables.py` prints them.
