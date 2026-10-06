# Live updates: measurements M1–M5 for the note on ag-grid's repaint

Date: 2026-10-05. Branch `claude/live-update-measure`, based on `60d522ed`; the measurements ran on
that branch's spikes before they were committed. No file under `src/` was changed.

This records measurements M1 to M5 proposed by the research note
`docs/research/ag-grid-rendering-on-data-change.md` (§8, "Measurements"), so that its candidates
can be decided on numbers rather than predictions. It decides nothing: no ADR, no `CONTEXT.md`
entry and no Definition of Done criterion is changed. Where a finding calls for a decision, it is
written as a proposal in the last section.

Performance never gates (AGENTS.md). M5 is a correctness check.

## The results in brief

| | What was measured | The number that matters |
|---|---|---|
| **M1** | A live tick in the browser (WebAssembly, headless Chrome): k of 40 painted rows replaced by new instances, keyed by instance (**A**, ExGrid today) or by a stable record key (**B**, candidate 2) | k = 40, 20 columns, 3 cells changed per row: render 16.0 ms (A) against 8.6 ms (B), frame 21.8 against 11.5 ms; 1,840 DOM mutation records against 180. B's render is 0.52–0.75 of A's in every configuration |
| **M2** | What a tick puts on a Blazor Server circuit, counted in .NET and read off the WebSocket | k = 40, 20 columns, 3 cells: 201,859 bytes (A) against 14,792 (B) per tick uncompressed; 40,712 against 3,134 on the wire with the default compression. The .NET render costs about the same in A and B (B is 0.81–0.98 of A) |
| **M3** | ExPivot's live redraw on `/pivot-live`'s generator | Every painted row (11 of 11) mounts on every redraw. The rows candidate 1 would keep, in `/pivot-live`'s layout with subtotals: a median of 5 of 11 at its own rate (5 changes per batch), none at 100 or more |
| **M4** | The bundled source under a Filter and a two-key Sort, k changes at 10⁵ and 10⁶ rows | At 10⁶ rows: one `ReplaceRow` 354 ms; one requery for a batch 365 ms (k = 1,000); the incremental prototype 3.2 ms (k = 1,000). The real grid's pass over a new Window of 451,115 rows: 18.1 ms |
| **M5** | The incremental prototype against `GridQueryEngine.Apply` | Equal in every one of 108,000 random batches over 27,000 cases, the stable tie order included; four defects planted on purpose were each caught |

Per candidate (detail in "The candidates against their predictions"):

| Candidate | Verdict |
|---|---|
| 1. ExPivot keeps unchanged report rows | **Partly held, partly wrong.** The share held (small at 1,000 changes). Wrong in "renders only the rows that changed": a kept row would still render while the Change Highlight is declared, because the grid is handed a new `CellChangedAt` delegate for every data version |
| 2. An opt-in key used only to keep a row's component | **Held**, on all four of its predictions |
| 3. A bundled live Grid Source | **Open** (its predictions are not about cost). M4 adds: no requery of the whole result meets "1,000 changes ≤ 0.2 s" at 10⁶ rows; the incremental path does |
| 4. Incremental requery | **Held** that it wins for few changes and loses for many (here between 10% and 50% of 10⁵ rows). **Wrong** that it is not worth it on a handful of rows: at k = 1 it is 800 to 1,600 times faster. M5: correct |
| 5. Grid-side work per new Window | **Held** (linear: 0.66 → 10.9 ms for the check alone, 1.45 → 18.1 ms in the real grid). New: once candidate 4 is in, it is the largest cost of an update |

## Environment

- **Machine.** Apple M4 Pro, 12 cores, 24 GB, macOS 26.6.2 (25G83). The machine was not quiet:
  load average 4.2–7.5 during the browser runs (noted beside each run).
- **.NET.** SDK 10.0.203 through `nix develop`, runtime 10.0.7, Release builds. ASP.NET Core
  Components 10.0.7 in the render-bench spikes (ExGrid itself references 10.0.0 and resolves to
  10.0.7 there).
- **Browser.** Google Chrome 154.0.8037.93, `--headless=new`, driven over the DevTools Protocol on
  its own port (9391) and profile; Node 24.14.1. Device pixel ratio 1, window 756 × 469.
- **WebAssembly build.** Release IL, no AOT (`spikes/render-bench` as it always ran).
- **Headless numbers are software-rendered.** They are read as A against B, never as absolutes. A
  run on real hardware (headed, a real GPU) is the user's to do by hand (below).
- The host ports were checked free first; only the hosts this run started were stopped, by PID.

## M1 — a live tick in the browser

**Method.** A new page, `/live`, in `spikes/render-bench` (its README says how to run it), over a
new Razor library `Bench.Live`:

- `LiveRow` is ExGrid's row shape in miniature, as the `RowComponent` mode is: one component per
  row whose `ShouldRender` is written by hand and compares the row by reference, and cells as plain
  markup. Each cell carries what `ExGridRow` writes: class (interned, with a tone class for a
  negative value), style, role, id (composed once per cell for the row's life, as `ExGridRow`
  caches it) and aria-colindex. One label cell and 20 or 50 number cells.
- `LiveGrid` paints 40 rows. **A** keys each row by an object held per instance in a
  `ConditionalWeakTable`, as `ExGrid.razor:446` and `:8681` do. **B** keys it by the record's Id,
  and `ShouldRender` still compares the row by reference.
- A tick replaces k = 1, 5 or 40 rows by new instances with the same Id, with 3 painted values
  moved per row, all of them, or none (a value-identical new instance), and hands over a new Window
  array. Both grids are on the page and get the same ticks from one seed, interleaved A, B, A, B.
- Per tick, as `/format` measures: **render** (Blazor's render and the DOM update, stamped in
  `OnAfterRender`), **layout** (forced right after it), **paint** (the next frame's main-thread
  paint and commit); **frame** is their sum. A MutationObserver per grid counts records, nodes
  added and removed, attribute and text changes. Blazor sets a new element's attributes before
  inserting it, so those attribute writes are not seen; M2 counts them. The rows count their own
  mounts and renders. After each configuration, the two grids' text and classes are compared: B,
  updated in place, ended where A rebuilt, in every configuration ("Same DOM": yes).
- 300 measured ticks per series after 30 warm-up ticks. Run 1 at load average 4.2–4.7, run 2
  at 4.8–6.9, and a third run for "0 cells" at 5.2–7.5.

**Results** (milliseconds, medians unless said; mutation counts are means per tick and are
deterministic):

| Cols | k | Changed cells | Render A (run 1 / 2) | Render B (run 1 / 2) | B/A | Render p95 A / B | Frame A | Frame B | Mutation records A / B | Nodes added A | Attr. / text changes B |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 20 | 1 | 3 | 1.3 / 1.0 | 0.9 / 0.7 | 0.69 | 1.6 / 1.1 | 4.3 | 3.6 | 46 / 4 | 44 | 1 / 3 |
| 20 | 5 | 3 | 3.1 / 2.6 | 1.8 / 1.5 | 0.58 | 3.4 / 1.9 | 6.3 | 4.3 | 230 / 23 | 220 | 8 / 15 |
| 20 | 40 | 3 | 16.0 / 16.1 | 8.6 / 8.8 | 0.54 | 16.7 / 8.8 | 21.8 | 11.5 | 1,840 / 180 | 1,760 | 60 / 120 |
| 50 | 1 | 3 | 1.7 / 1.4 | 1.1 / 0.9 | 0.65 | 1.9 / 1.2 | 5.8 | 4.3 | 106 / 5 | 104 | 2 / 3 |
| 50 | 5 | 3 | 5.0 / 4.8 | 2.8 / 2.7 | 0.56 | 5.3 / 2.9 | 9.7 | 6.1 | 530 / 23 | 520 | 8 / 15 |
| 50 | 40 | 3 | 33.4 / 33.5 | 18.0 / 18.1 | 0.54 | 35.0 / 18.4 | 44.9 | 22.3 | 4,240 / 180 | 4,160 | 60 / 120 |
| 20 | 1 | all | 1.2 / 1.0 | 0.9 / 0.7 | 0.75 | 1.5 / 1.1 | 4.4 | 3.8 | 46 / 30 | 44 | 10 / 20 |
| 20 | 5 | all | 3.1 / 2.6 | 1.9 / 1.6 | 0.61 | 3.3 / 2.0 | 6.3 | 4.8 | 230 / 150 | 220 | 50 / 100 |
| 20 | 40 | all | 16.1 / 16.0 | 9.3 / 9.3 | 0.58 | 17.1 / 9.6 | 22.4 | 14.5 | 1,840 / 1,201 | 1,760 | 401 / 800 |
| 50 | 1 | all | 1.8 / 1.5 | 1.2 / 1.0 | 0.67 | 2.2 / 1.4 | 4.9 | 3.5 | 106 / 75 | 104 | 25 / 50 |
| 50 | 5 | all | 5.1 / 4.7 | 3.1 / 2.9 | 0.61 | 5.4 / 3.3 | 8.7 | 5.8 | 530 / 375 | 520 | 125 / 250 |
| 50 | 40 | all | 33.7 / 34.1 | 20.1 / 20.4 | 0.60 | 35.7 / 21.3 | 44.6 | 29.6 | 4,240 / 2,999 | 4,160 | 999 / 2,000 |
| 20 | 1 | 0 | 1.0 | 0.7 | 0.70 | 1.2 / 0.8 | 3.7 | 0.7 | 46 / 0 | 44 | 0 / 0 |
| 20 | 5 | 0 | 2.7 | 1.5 | 0.56 | 3.0 / 1.7 | 5.9 | 1.6 | 230 / 0 | 220 | 0 / 0 |
| 20 | 40 | 0 | 16.0 | 8.4 | 0.52 | 17.0 / 8.6 | 21.5 | 8.4 | 1,840 / 0 | 1,760 | 0 / 0 |
| 50 | 1 | 0 | 1.5 | 0.9 | 0.60 | 1.7 / 1.0 | 5.8 | 1.0 | 106 / 0 | 104 | 0 / 0 |
| 50 | 5 | 0 | 4.8 | 2.6 | 0.54 | 5.1 / 2.8 | 9.6 | 2.7 | 530 / 0 | 520 | 0 / 0 |
| 50 | 40 | 0 | 33.7 | 17.8 | 0.53 | 35.9 / 18.4 | 44.2 | 17.9 | 4,240 / 0 | 4,160 | 0 / 0 |

B/A is run 1's render medians. A mounted k rows per tick and rendered k; B mounted none and
rendered k. A removed 2 nodes per replaced row (the row and the whitespace text after it).

**What it shows.**

- B halves the render at k = 40 whatever the number of changed cells (0.52–0.60), and the frame
  falls further where few cells change (0.50–0.53 at 3 cells) than where all do (0.65–0.66).
- B with **no** changed cell still costs half of A's render (8.4 against 16.0 ms at k = 40, 20
  columns) and does nothing to the DOM: that half is the .NET render in WebAssembly's interpreter,
  which A and B share, and the other half of A is building the DOM. Its frame is 0.17–0.40 of A's.
  This is what a row costs that is handed over again as a new instance with the same values — and
  what a kept row costs that renders for a reason other than its values (see candidate 1).

Raw: `spikes/render-bench/results/20261005-155831-105.json` (run 1),
`20261005-161804-308.json` (run 2), `20261005-162115-849.json` (0 and all cells), every tick's
render time included; the driver's tables in `raw/m1-run*-driver.txt`.

## M2 — what a tick carries over a circuit

Three measurements of the same ticks (same rows, seeds and markup as M1):

1. **Counted in .NET** (`spikes/render-bench/Bench.BatchCount`). `LiveGrid` rendered by a test
   `Renderer` that captures each `RenderBatch` — what Blazor Server hands its `RenderBatchWriter`
   — and counts edits, reference frames and strings. Its byte figure is an estimate: the writer is
   internal, so its layout is transcribed (16 bytes per edit, 20 per frame, strings deduplicated
   only where the writer deduplicates them). The .NET render is timed with the counting off, A and
   B interleaved tick by tick, 1,000 ticks after 1,000 warm-up, the whole matrix run twice and the
   second pass kept (the first was still tiering up). 1,000 ticks per series for the counts.
2. **Measured on the wire** (`spikes/render-bench/Bench.Server` and `tools/cdp-ws.mjs`). The same
   two grids on Blazor Server, one tick per click, with the page itself never re-rendering. For each
   tick: the `JS.RenderBatch` message as Chrome reports it (the SignalR BlazorPack message,
   decompressed) and the bytes Kestrel wrote to the circuit's TCP connection during the tick,
   counted in a connection middleware (after per-message compression, with no TLS on this host). A
   tick is over when one more render batch has arrived and one more `OnRenderCompleted` has gone
   back. 50 ticks per series after 5 warm-up, with compression on (the default here:
   `permessage-deflate; client_max_window_bits=15`) and off (`DisableWebSocketCompression`). The
   wire figure also holds the event's small completion message (`JS.EndInvokeDotNet`), alike in A
   and B. The browser's own messages per tick (the click and `OnRenderCompleted`) were 383–387
   bytes in A and in B.
3. **Calibrated against the real ExGrid** (`Bench.BatchCount calibrate`). The same ticks rendered by
   `ExGrid<T>` itself (variant A, its only keying), with a JavaScript runtime that answers every
   call with a default and a Viewport that paints all 40 rows and every column.

**Results** (medians per tick; counts are means, deterministic):

| Cols | k | Changed | Var. | Edits | Frames | Estimated bytes | Payload bytes | Wire, no compression | Wire, compressed | .NET render ms (p95) |
|---|---|---|---|---|---|---|---|---|---|---|
| 20 | 1 | 3 | A | 5 | 157 | 5,267 | 5,296 | 5,339 | 681 | 0.010 (0.011) |
| 20 | 1 | 3 | B | 12 | 4 | 430 | 471 | 510 | 88 | 0.009 (0.010) |
| 20 | 5 | 3 | A | 17 | 785 | 25,429 | 25,457 | 25,521 | 3,629 | 0.025 (0.030) |
| 20 | 5 | 3 | B | 62 | 22 | 1,895 | 1,927 | 1,967 | 381 | 0.021 (0.024) |
| 20 | 40 | 3 | A | 122 | 6,280 | 201,816 | 201,859 | 202,095 | 40,712 | 0.145 (0.166) |
| 20 | 40 | 3 | B | 500 | 180 | 14,716 | 14,792 | 14,844 | 3,134 | 0.117 (0.129) |
| 50 | 1 | 3 | A | 5 | 367 | 12,111 | 12,138 | 12,186 | 1,751 | 0.015 (0.018) |
| 50 | 1 | 3 | B | 13 | 5 | 432 | 431 | 471 | 113 | 0.013 (0.014) |
| 50 | 5 | 3 | A | 17 | 1,835 | 59,650 | 59,697 | 59,793 | 12,193 | 0.044 (0.051) |
| 50 | 5 | 3 | B | 62 | 22 | 1,894 | 1,937 | 1,977 | 485 | 0.038 (0.044) |
| 50 | 40 | 3 | A | 122 | 14,680 | 475,618 | 475,600 | 476,104 | 95,878 | 0.306 (0.367) |
| 50 | 40 | 3 | B | 500 | 180 | 14,712 | 14,790 | 14,842 | 3,256 | 0.254 (0.295) |
| 20 | 1 | all | A | 5 | 157 | 5,267 | 5,286 | 5,330 | 749 | 0.011 (0.012) |
| 20 | 1 | all | B | 72 | 30 | 2,252 | 2,274 | 2,314 | 333 | 0.010 (0.011) |
| 20 | 5 | all | A | 17 | 785 | 25,429 | 25,451 | 25,515 | 5,414 | 0.025 (0.028) |
| 20 | 5 | all | B | 360 | 150 | 11,016 | 10,925 | 10,973 | 1,905 | 0.022 (0.025) |
| 20 | 40 | all | A | 122 | 6,280 | 201,833 | 201,884 | 202,120 | 40,736 | 0.146 (0.170) |
| 20 | 40 | all | B | 2,880 | 1,200 | 87,717 | 87,632 | 87,756 | 17,033 | 0.135 (0.152) |
| 50 | 1 | all | A | 5 | 367 | 12,111 | 12,138 | 12,187 | 2,034 | 0.015 (0.015) |
| 50 | 1 | all | B | 177 | 75 | 5,477 | 5,509 | 5,554 | 901 | 0.014 (0.015) |
| 50 | 5 | all | A | 17 | 1,835 | 59,648 | 59,675 | 59,772 | 12,204 | 0.046 (0.053) |
| 50 | 5 | all | B | 885 | 375 | 27,139 | 27,168 | 27,233 | 5,810 | 0.045 (0.051) |
| 50 | 40 | all | A | 122 | 14,680 | 475,604 | 475,644 | 476,149 | 95,831 | 0.305 (0.411) |
| 50 | 40 | all | B | 7,079 | 2,999 | 216,569 | 216,753 | 217,002 | 42,387 | 0.299 (0.325) |

A disposed k components per tick and B none. The estimate agrees with the measured payload within
0.5% wherever a batch is over 10 KB, and within about 40 bytes (the SignalR message's framing) on
the small ones — B's content varies from tick to tick, and the estimate is a mean over 1,000 ticks
where the payload is a median over 50 — so the transcription can stand in for the writer's output in
this spike.

**The real ExGrid's rows against the mirror's.** With the same ticks, the real grid's remount
carries 1.07–1.18 times the mirror's estimated bytes when every number fits its column (160 px):
6,230 bytes for one 20-column row (mirror 5,267), 224,485 for 40 such rows (mirror 201,816),
506,648 for 40 rows of 50 columns (mirror 475,618) — one attribute more per cell. At the mirror's
90 px most of these seven-digit numbers do not fit and the real grid paints them as `####`
(ADR-0016), with a span and an aria-label in the cell; its remount is then 1.46–1.54 times the
mirror's (8,123; 299,291; 693,409 bytes). What the real grid would send under B is not measured: it
needs `ExGrid.razor`'s `RowKey` changed (see the proposals).

Raw: `raw/m2-batchcount.json` (with edits and frames by type), `raw/m2-server-ws-compression-on.json`
and `-off.json` (every tick), `raw/m2-realgrid-calibration.json` (160 px) and
`raw/m2-realgrid-calibration-90px.json`.

## M3 — ExPivot's live redraw

**Method** (`spikes/live-update/PivotRedraw`). ExPivot in bUnit, as `tests/ExPivot.Components`
renders it (the same JavaScript stand-ins, an interactive Server renderer, a `FakeTimeProvider`),
over the bundled source of `/pivot-live`'s trades: `DemoPivotData`'s generator, fields and
`PivotLivePage`'s amendment (a trade's P&L moved by up to 0.1% of its notional), copied rather
than referenced so as not to pull in the demo pages. `RedrawInterval` 250 ms, Change Highlight 1 s,
`ViewportHeight` 300 as on `/pivot-live` (and `ViewportWidth` 900, which `/pivot-live` leaves to the page), no slicing. Each redraw: the clock moves on 250 ms, one
Change Batch is applied, and the report on screen is waited for (a new report instance — a
condition, not a time). Then, among the report grid's painted rows (`ExGridRow` components):
**mounted** (an instance not painted before), **rendered** (mounted, or rendered again), and
**unchanged** — its row stands for what a row of the previous report stood for (role, Value Field
and Items, as `ReportHistory` pairs them) and every cell it paints, labels and value texts, reads
the same under the same columns. "Unchanged" is what candidate 1 would keep. 300 redraws per
configuration.

Two layouts: `/pivot-live`'s own (Region > Desk by Product, filtered by Currency: 14 report rows)
and a taller one (Book > Month by Product: 101 rows), each with the outer field's subtotals on and
off.

**Results.** In every configuration and every redraw, **11 rows were painted, 11 mounted and 11
rendered** (minimum = maximum). The unchanged painted rows, median (mean) [min–max], and the
unchanged rows of the whole report (median):

| Layout | Subtotals | Trades | Changes per batch | Unchanged painted rows of 11 | Unchanged report rows |
|---|---|---|---|---|---|
| Region > Desk (14 rows) | on | 2,000 | 5 (`/pivot-live`'s rate) | 5 (4.99) [3–9] | 6 of 14 |
| Region > Desk | on | 2,000 | 10 | 3 (2.63) [0–6] | 3 of 14 |
| Region > Desk | on | 2,000 | 100 | 0 [0–0] | 0 |
| Region > Desk | on | 2,000 | 1,000 | 0 [0–0] | 0 |
| Region > Desk | on | 1,000,000 | 1,000 | 0 [0–0] | 0 |
| Region > Desk | off | 2,000 | 5 | 8 (7.61) [6–10] | 9 of 14 |
| Region > Desk | off | 2,000 | 10 | 6 (5.59) [3–8] | 6 of 14 |
| Region > Desk | off | 2,000 | 100 | 3 (3.00) [3–3] | 3 of 14 |
| Region > Desk | off | 2,000 | 1,000 | 3 [3–3] | 3 of 14 |
| Region > Desk | off | 1,000,000 | 1,000 | 3 [3–3] | 3 of 14 |
| Book > Month (101 rows) | on | 2,000 | 5 | 10 (9.78) [7–11] | 91 of 101 |
| Book > Month | on | 2,000 | 10 | 9 (8.87) [6–11] | 84 of 101 |
| Book > Month | on | 2,000 | 100 | 4 (3.72) [0–8] | 30 of 101 |
| Book > Month | on | 2,000 | 1,000 | 0 [0–0] | 0 |
| Book > Month | on | 1,000,000 | 1,000 | 0 [0–0] | 0 |
| Book > Month | off | 2,000 | 5 | 11 (10.60) [8–11] | 95 of 101 |
| Book > Month | off | 2,000 | 10 | 10 (10.20) [8–11] | 90 of 101 |
| Book > Month | off | 2,000 | 100 | 6 (5.72) [2–10] | 40 of 101 |
| Book > Month | off | 2,000 | 1,000 | 2 [2–2] | 10 of 101 |
| Book > Month | off | 1,000,000 | 1,000 | 2 [2–2] | 10 of 101 |

With subtotals off, the rows that stay unchanged under every rate are the outer Items' group rows,
which carry no values. The histograms per configuration are in the raw files.

**Why a kept row would still render.** ExPivot hands its grid a new `CellChangedAt` delegate for
every data version (`ReportHistory.Extend` makes a new history and with it a new `Answer`;
`src/ExPivot/Components/ExPivot.Report.cs:446`), and `ExGridRow.ShouldRender` compares that
delegate by reference (`src/ExGrid/Components/ExGridRow.razor:318`). The layer-2 test
`A_new_delegate_makes_the_painted_rows_ask_again`
(`tests/ExGrid.Components/ChangeHighlightTests.cs:198`) pins that behaviour (ADR-0068). So under
candidate 1, with the Change Highlight declared (its default is on), an unchanged row would keep its
component but render on every redraw, writing nothing to the DOM: M1's "0 changed cells" row B, at
0.52–0.70 of a remount's render and 0.17–0.40 of its frame in headless WebAssembly. This is read
from the code and the existing test; it was not measured inside ExPivot, which would need `src/`
changed.

Not run: ADR-0067's `Apply` → frame observation in a browser (a layer-3 measure spec), which the
note proposes after the counts. Layer 3 is CI's and the user's.

Raw: `raw/m3-pivot-redraw.json`, `raw/m3-pivot-redraw-5-changes.json` (with histograms).

## M4 — requery cost in the bundled source

**Method** (`spikes/live-update/Requery`, a Release console; `Stopwatch`, warm-up first, a GC
before each timed run, min and median of N runs, each from the same starting state). Trades with
200 books, six desks, a P&L (5% blank), a notional and a date. The Filter keeps about 45%: Desk In
{Rates, Credit, FX} and Notional ≥ 500,000. The Sort: Book ascending, then P&L descending. A tick
replaces k rows with new instances with a new P&L (so a row moves within its book), one in ten
also with a new notional (so it can cross the Filter).

- **(a) today:** k calls of `GridSource.From(rows).ReplaceRow` on a source already under the Filter
  and Sort. Where k calls would take minutes, the calls are timed one by one and the batch is the
  median call times k — marked "derived".
- **(b) one requery for the batch:** locate the k rows in one pass, replace them, one
  `GridQueryEngine.Apply`, and the sequence comparison with the replacements mapped across — what a
  batch method that requeries would cost.
- **(c) incremental** (`IncrementalQuery.cs`, ag-grid's `deltaSort` on the engine's semantics):
  each leaving row is found in the previous result by binary search on its own (keys, ordinal); the
  entering rows are filtered and sorted by `GridQueryEngine.Apply` itself, handed over in base
  order; each is placed by binary search; the result is merged block by block. Whether the sequence
  moved is answered from the k pairs alone. Ties fall in base order because every base row carries
  an ordinal that grows with its position.
- **grid:** `RequireDistinctRows` (`ExGrid.razor:2858-2875`) transcribed and timed over the new
  Window; and, in `Bench.BatchCount window`, the **real** `ExGrid` timed on a push of a new Window
  instance with one off-screen row replaced, against the same instance pushed again.

**Results** (milliseconds; median, with min where it helps):

| n (Window) | k | (a) today, k × ReplaceRow | (b) one requery | (c) incremental | (b)/(c) |
|---|---|---|---|---|---|
| 10⁵ (45,017) | 1 | 20.1 | 23.2 | 0.029 | 800 |
| 10⁵ | 100 | 2,438 (5 runs) | 25.3 | 0.186 | 136 |
| 10⁵ | 1,000 | 24,487 (1 run) | 24.7 | 1.05 | 24 |
| 10⁵ | 10,000 | not run | 25.8 | 13.7 | 1.9 |
| 10⁵ | 50,000 | not run | 31.3 | 59.6 | **0.53** |
| 10⁶ (451,115) | 1 | 354 | 370 | 0.234 | 1,580 |
| 10⁶ | 100 | 36,870 (1 run) | 362 | 0.591 | 612 |
| 10⁶ | 1,000 | ≈ 364,545 (derived: 1,000 × the median of 100 timed calls, 364.5) | 365 | 3.19 | 114 |
| 10⁶ | 10,000 | not run | 389 | 29.7 | 13 |
| 10⁶ | 100,000 | not run | 406 | 250 | 1.6 |

`GridQueryEngine.Apply` alone: 22.6 ms at 10⁵, 349 ms at 10⁶. One `ReplaceRow` call, median: 20–24
ms at 10⁵, 354–365 ms at 10⁶.

The grid's side, per new Window instance:

| Window rows | `RequireDistinctRows`, transcribed | Real ExGrid, new instance (one off-screen row replaced) | Real ExGrid, same instance again |
|---|---|---|---|
| 45,017 | 0.66 (min 0.50) | 1.45 (min 1.10, max 52.6) | 0.17 |
| 451,115 | 10.9 (min 10.6) | 18.1 (min 15.9, max 30.9) | 0.10 |

The real grid's pass costs more than the check transcribed (18.1 against 10.9 ms at 451,115 rows).
`ApplyState` walks the Window only in `RequireDistinctRows` (read from the code); where the rest
goes was not profiled.

All of this is .NET on desktop CoreCLR; under WebAssembly's interpreter every figure would be
larger, and that was not measured.

Raw: `raw/m4-requery.json` and `.log`, `raw/m4-requery-large-k-1e5.json`, `-1e6.json`,
`raw/m4-grid-window-pass.json`.

## M5 — the incremental result equals the full requery

**Method** (`PropertyCheck.cs`, run before any timing, and by `time` itself before it times
anything). Random bases of 0–400 rows; random Sorts of 0–3 levels over Number, Text, Date and
Boolean columns in either direction; random Filters of 1–2 columns with 1–2 clauses, And or Or, over
every operator each type allows (In with Blanks among its values, IsBlank, Contains, and the
comparisons); random batches of replacements, additions and removals, from none to every row, and
replacements that change nothing. Values chosen for the corners: blanks, ties (`1`, `1.0`, `1.00`),
`decimal.MaxValue`, case pairs, `ä`/`Ä`, `ß` against `ss`, the Turkish `i`/`ı`/`İ`, a precomposed
`é` against `e` + combining accent, the empty string, equal `DateTime`s of different `Kind`. After
every batch the incremental Window is compared with `GridQueryEngine.Apply` over the new base,
position by position, by reference; its "sequence moved" answer with the definition written out over
every position; and in a third of the cases (replacements only), the Window with
`GridSource.From(...).ReplaceRow` applied one replacement at a time.

**Results.** Seed 20261005: 5,000 cases, 19,971 batches, 1,612,848 rows compared. Seed 4242:
20,000 cases, 80,010 batches, 6,504,260 rows compared, 6,636 of them also against `ReplaceRow`.
Seed 7 (before each timing run): 2,000 cases, 7,966 batches. **No difference in any batch.** The
batches cover 0–3 sort levels about equally, filtered and unfiltered, empty batches, adds and
removes, the result changing size (25,955 batches), and the sequence kept (47,460) and moved
(32,550).

**The check catches what it should.** Four defects planted on purpose (`REQUERY_FAULT`) were each
caught at once: blanks first, case-sensitive text, culture-aware text, and ties in reverse order.

Raw: `raw/m5-check-seed-*.txt`, `raw/m5-planted-faults.txt`.

## The candidates against their predictions

**Candidate 1 — ExPivot keeps the instances of report rows whose painted values did not change.**

- *"A live redraw renders only the rows that changed, instead of remounting every painted row."*
  The second half is confirmed: today every painted row mounts on every redraw (M3, 11 of 11
  throughout). The first half is **wrong as stated**: with the Change Highlight declared, a kept row
  still renders on every redraw, because the delegate is new per data version (M3, "Why a kept row
  would still render"). What candidate 1 alone saves is a remount turned into a render that writes
  nothing to the DOM — about half the render and a fifth to two fifths of the frame per row in
  headless WebAssembly (M1, 0 changed cells), and on a circuit the row's whole remount payload
  (M2: 5.3 KB per 20-column row in the mirror, 6.2–8.1 KB in the real grid, uncompressed).
- *"The share of painted rows that change per redraw is what decides the gain … With 1,000 changes
  … the gain would be small."* **Held.** At 1,000 changes nothing is kept but rows that carry no
  values. At `/pivot-live`'s own rate (5 changes per batch over 2,000 trades) about half the painted
  rows of its own layout are unchanged, and nearly all of a taller report's.

**Candidate 2 — an opt-in key used only to keep a row's component.**

- *"The browser's DOM work falls from the row's every element, attribute and text to its changed
  texts and classes."* **Held** (M1): no node added or removed; one text change per changed cell
  and a class change where the tone flipped.
- *"The .NET render cost of the row stays about the same."* **Held** (M2, no DOM): B is 0.81–0.98
  of A. In headless WebAssembly, render and DOM update together, B is 0.52–0.75 of A (M1).
- *"A component construction and `OnInitialized` are saved."* **Held**: B mounts no row.
- *"On Blazor Server, the bytes per update fall in the same proportion."* **Held**, in direction
  and order: with 3 changed cells per row the payload falls to 0.07 (20 columns) and 0.03 (50
  columns) of A's, and the DOM mutation records to 0.10 and 0.04; with every cell changed, to
  0.43–0.46 and 0.65–0.71. §5.5's prediction — bytes grow with every painted cell of a changed row
  under remounting and with the changed cells alone under keeping — **held**: A's payload does not
  depend on how many cells changed; B's does.

**Candidate 3 — a bundled live Grid Source over a Snapshot and Change Batches.** **Open**: its
predictions are about Consumer code and guarantees, which no measurement settles. M4 bears on its
cost: stated for ExGrid, PV-21's "1,000 changes ≤ 0.2 s" is missed at 10⁶ rows by today's path
(about six minutes, derived) and by one requery of the whole result (365 ms), and met by the
incremental path (3.2 ms, plus the grid's 18.1 ms pass), in .NET on this machine.

**Candidate 4 — incremental requery.**

- *"It wins when the changed rows are few against the result."* **Held**: 24–1,580 times faster
  than one requery up to k = 1,000.
- *"… and loses for batches large against the data."* **Held**: at 10⁵ rows it loses at
  k = 50,000 (59.6 against 31.3 ms) and still wins at 10,000; at 10⁶ it still wins at 100,000
  (250 against 406 ms). The crossover was not looked for more finely.
- *"… and is not worth its overhead on a handful of rows, where ag-grid falls back to a full
  sort."* **Wrong** for this prototype: at k = 1 it takes 0.029 ms against 23 ms (10⁵) and 0.23
  against 370 ms (10⁶).
- M5: **correct** in every batch.

**Candidate 5 — grid-side work per update that grows with the Window.** *"Linear in the Window:
small at today's demo sizes, and visible at 10⁶ rows with frequent `ReplaceRow` calls."* **Held**:
the check alone 0.66 → 10.9 ms, the real grid's pass 1.45 → 18.1 ms, from 45,017 to 451,115 rows. Today
it is about 5% of a `ReplaceRow` at 10⁶ (354 ms). Not predicted: once candidate 4 is in, it is
**the largest cost of an update** — 18.1 ms in the grid against 0.23–3.2 ms in the source.

## Surprises

- A row handed over again with **no value changed** costs half a remount under B in headless
  WebAssembly (M1): the .NET render in the interpreter is half of A's cost, and B still pays it. A
  kept row is cheaper than a remounted one, but not free.
- Candidate 1 does not, by itself, stop unchanged pivot rows rendering: the Change Highlight's
  delegate identity renders them anyway (M3).
- On a circuit, today's remount of 40 rows of 20 columns is 202 KB per tick uncompressed and 41 KB
  compressed in the mirror; the real grid's rows are 1.07–1.18 times heavier, and 1.5 times where
  numbers overflow into `####` (224–299 KB estimated uncompressed). Under B with 3 changed cells per
  row it is 15 KB and 3 KB.
- One `ReplaceRow` at 10⁶ rows (354 ms) alone misses 0.2 s.
- The incremental path's advantage is largest exactly where the note expected it to be not worth it,
  at a single change.

## Not measured, and why

- **Real hardware.** Headless numbers only. To take them by hand: in `spikes/render-bench`,
  `nix develop -c dotnet run -c Release --project Bench.Host --urls http://127.0.0.1:5199`, open
  <http://127.0.0.1:5199/live> in Chrome or Edge, press **Measure live ticks**, then **Save results
  to the server**.
- **Candidate 2 in the real ExGrid** (variant B): needs `RowKey` in `ExGrid.razor` changed.
- **Candidate 1 inside ExPivot** (rows actually kept, renders counted): needs `src/ExPivot` changed.
- **ADR-0067's `Apply` → frame** for candidate 1, and anything on Windows or with a real IME:
  layer 3 and hand runs, not run here.
- **WebAssembly's cost of a requery** (M4 is desktop CoreCLR only).

## Reproducing

```sh
# M5 and M4 (spikes/live-update/Requery)
nix develop -c dotnet run -c Release --project spikes/live-update/Requery -- check 20000 4242
nix develop -c dotnet run -c Release --project spikes/live-update/Requery -- time out.json
nix develop -c dotnet run -c Release --project spikes/live-update/Requery -- time out.json 100000 10000,50000 no-a
REQUERY_FAULT=ties nix develop -c dotnet run -c Release --project spikes/live-update/Requery -- check 3000 11   # must fail

# M3 (spikes/live-update/PivotRedraw)
nix develop -c dotnet run -c Release --project spikes/live-update/PivotRedraw -- out.json 300

# M1 and M2: spikes/render-bench/README.md, "A live tick (/live …)"
```

Layers 1 and 2 were run on this branch before committing (`nix develop -c dotnet test ExGrid.slnx`):
every suite passed, as before; nothing under `src/` or `tests/` changed.

## Proposals that need the user's decision

1. **Candidate 1 and the Change Highlight's delegate.** If ExPivot is to keep unchanged rows, decide
   whether the grid may also be handed the same `CellChangedAt` delegate across data versions for
   them. Without that, the gain measured is a remount turned into a render (M1, 0 cells), not a row
   that skips. With it, ADR-0068's "a new delegate is the change signal" and `ReportHistory`'s
   lookup by `row.Report` need rereading, beside the contract "a new report is new rows" that the
   note already raises (Open question 2).
2. **Candidate 5 becomes the bottleneck after candidate 4.** If incremental requery (or a live
   source built on it) is taken, the grid's pass over every new Window instance — 18 ms at 451,115
   rows — is then the largest cost of an update. The refusal it makes is spine 1's and stays; a
   cheaper route to it (for example a check over the positions a bundled source says moved) is a
   decision, best taken with candidate 4's ADR.
3. **The batch entry point is the first win, incremental the second.** One requery per batch (b)
   alone turns 1,000 changes at 10⁵ rows from 24.5 s into 25 ms; at 10⁶ it is still 365 ms, and only
   the incremental path meets 0.2 s. Open question 6 (an ADR for a batch method on `GridSource.From`,
   or riding on candidate 3's) can be decided with these numbers; so can a fallback to one full
   requery for batches large against the data (the crossover here lies between 10% and 50% of 10⁵).
4. **Candidate 2 in the real grid.** The mirror's B is measured; the real ExGrid's B is not, and its
   rows are 1.07–1.5 times heavier to remount. If that number is wanted before Open question 1 is
   decided, it needs a throwaway branch that changes `RowKey` in `src/` for measurement only, never
   merged — the user's call, since it touches `src/`.
5. **Hand runs.** M1 on real hardware (Chrome or Edge, headed), and on Windows; the layer-3
   `Apply` → frame observation if candidate 1 goes ahead.
