# 14: A remote report's Window is read ahead

Status: ready-for-agent

**What to do:** measure what reading ahead would buy a report served over a network. Then, once the
user has decided, build it: ExPivot asks a remote report source for more rows than the grid needs. Ordinary
scrolling, and a key that scrolls by a row, are then painted from rows already held, not from Placeholders
while a round trip is out.

**Blocked by:** none in this spec. It builds on ADR-0151's report Windows, which reach `main` with PR #70.

## Where it stands

*(Read on `claude/live-data-best`, 2026-10-10.)*

- **The grid asks for the visible range, and only when its Window does not cover it.** That range is the
  rows one Viewport holds, with nothing either side (ADR-0001). A fling asks once it settles (ADR-0004).
  Until a Window that covers the range arrives, the grid paints Placeholders.
- **ExPivot asks its report source for exactly that range.** Its first Window is 64 rows, and every later
  one is the range the grid asked for. A newer Window question cancels the one in flight, and that
  question's answer is discarded.
- **A report source answers a moved Window from the report it holds.** No question is asked again, no
  report is laid out, and the Report Version stays the same. The answer is a complete Window, because its
  Baseline Window was another one. It carries the report's metadata, every column's definition included.
- **So over a network, each scroll that shows a row outside the Window costs a round trip.** The rows it
  shows stay Placeholders until the answer lands. Nothing is recomputed; the cost is the network's latency.
- **Two cases pay no network round trip here, and reading ahead would buy them nothing:**
  - a local report source;
  - ExPivot on Blazor Server, where ExPivot and its report source share the server. There, the circuit's
    round trip is the grid's own rendering, which reading ahead does not touch.
- **ExGrid's bundled fetching source already reads ahead.** `GridSource.Fetch` widens a Range Request by
  `readAheadRows` either side (60 by default), clamped to the total. It refuses an answer that does not
  reach the range the grid needed (ADR-0025). ExPivot's remote path is the family's one Window-fetching path
  without read-ahead.

## What to measure first

The numbers are observational and never gated, and they are recorded in `verification/`. Add a mode and
measure rather than assert from reasoning. Use a remote report source in a published WebAssembly build,
with 150 ms added to each round trip as in ADR-0151's experiment. Measure at 0, 30, 60 and 120 rows read
ahead:

- **How long Placeholders are on screen** while scrolling by the wheel, by a key that scrolls by a row,
  and by PageDown.
- **The bytes of a Window's answer**, for a narrow report and for one with hundreds of value columns.
  Every row carries every column.
- **A live update over the wider Window:**
  - the rows and bytes of its Window Changes;
  - the server's time to project the Window, since every row of the Window is projected for each update.
- **The Windows the report source keeps** for the versions it keeps (LV-22).

## Decisions it needs

These are the user's, once the measurement is in.

- **Whether to read ahead at all, and how many rows by default.**
- **Where the number lives.** ADR-0025 makes read-ahead a number the Consumer passes to the fetching
  source, because how far ahead to fetch is a judgement about their data and their server.
  - The same rule here would make it an argument of `PivotReportSource.Fetch`.
  - The alternative is a parameter of ExPivot.
- **Where it is recorded:** a note in ADR-0151, or an ADR of its own from this branch's block. ADR-0162 is
  next.

## Done when

- [x] The measurement is recorded, and the user has decided
- [x] The decision is in an ADR, and the Definition of Done says what holds (ADR-0182, LV-34)
- [x] Built, and each of these holds:
  - a scroll within the read-ahead asks nothing;
  - a scroll past it asks once, for the widened Window clamped into the report;
  - an answer that does not reach the rows the grid needs is refused, as ADR-0025 refuses one;
  - Window Changes and their Window Digest cover the whole widened Window
- [x] Layers 1 and 2 are green
- [ ] Layer 3 counts the report source's Window questions while it scrolls, and never reads a time.
  `/pivot-db`'s status line already counts the questions of each kind.
- [x] The Docs Site's ExPivot Sources page says it, and its server Example passes the number

## Comments

2026-10-10: Written up with the user after PR #70's review. It started from two questions. Does
scrolling a server's report recompute it? It does not. Would reading ahead serve the user better?

2026-10-11: Measured. The record is
[`verification/2026-10-10-linux-remote-read-ahead`](../../../../verification/2026-10-10-linux-remote-read-ahead/README.md).
The prototype and harness are on `lab/remote-read-ahead` at `f4ec680`, never merged. The setup:
- a published WebAssembly build, Playwright's bundled Chromium on a Linux container;
- the demo API server behind `latency-proxy.mjs`, at 150 ms a round trip;
- 0, 30, 60 and 120 rows read ahead.

It covers everything "What to measure first" asks. The first box stays unticked: it also says the user
has decided, and the decisions above are still the user's. What it found:
- **Without read-ahead, every row a scroll brings in is a Placeholder for a round trip** (a median 164–184
  ms). ArrowDown pressed one at a time met one 480 times in 480 rows. Held keys and wheel showed
  Placeholders 96–100% of the scroll's time, each input superseding the question before it.
- **Read ahead, an episode is as long, and rarer.** ArrowDown met one every N + 1 rows. Held wheel and
  ArrowDown showed Placeholders for 36–38% and 16–19% of the time at 30, 19–21% and 7–8% at 60, and 8–11%
  and 3–4% at 120. Held PageDown, about 300 rows a second, still did for 63–72% at 60.
- **This holds only if a question in flight whose Window covers the range is kept.** Superseding it as
  `GridSource.Fetch` does (ADR-0025), 60 rows left held scrolling at 85–88%.
- **An answer weighs its rows times its columns, plus the report's metadata every time.** The browser
  parses it on its one thread:

  | Report | 0 rows read ahead | 120 rows read ahead |
  |---|---|---|
  | narrow (6 columns) | 15 KiB of JSON, 5 ms to parse | 151 KiB, 43 ms |
  | wide (251 columns, sparse) | 91 KiB, 16 ms | 832 KiB, 93 ms |
  | dense (251 columns, a million trades) | 282 KiB, 76 ms | 3.9 MiB, 675 ms |
- **A live update carries more rows as the Window widens.** With 1,000-trade ticks its Window Changes held
  0, 5, 9 and 18 rows at 0 to 120, and nearly the whole Window over the dense report. The server projects
  every row of the Window for each update, changed or not: 0.3 to 3.3 ms narrow, 9 to 175 ms over 251
  columns.
- **The source keeps two Windows (LV-22), each read ahead:** up to 0.24, 1.3 and 12.6 MiB a report at 120.

2026-10-11, decided with the user, from the record of 2026-10-10:
- **A question whose Window covers the range stays in flight.** A newer question does not supersede
  it. This departs from ADR-0025's rule for `GridSource.Fetch`, and the ADR says why: under that
  rule, 60 rows ahead still left Placeholders 85–88% of a held scroll.
- **The number lives on the source,** as an argument of `PivotReportSource.Fetch`, as ADR-0025 puts
  `readAheadRows` on the bundled fetching source. A local source declares none.
- **It is recorded in an ADR of its own, ADR-0182,** from `claude/backlog-cleanup`'s block. The 0162
  named above belonged to `claude/live-data-best`'s block. That branch has merged, so its unused
  numbers stay unused (`docs/agents/numbering.md`).
- **The default is not decided yet.** The user asked how ag-grid compares. Its Server-Side and
  Infinite row models fetch aligned blocks of `cacheBlockSize` = 100 rows. A block is asked for when
  the rendered rows, the Viewport and `rowBuffer` = 10 either side, reach a block not yet held, and
  every block is kept (`maxBlocksInCache` = -1). Its Viewport row model adds
  `viewportRowModelBufferSize` = 5 either side, rounded out to pages of 5. These defaults are from
  `packages/ag-grid-community/src/gridOptionsDefault.ts`, ag-grid `latest` at
  12c6bb02b11f2e7f2b6451f6bbb9ee267339603b. Before choosing, the user asked for aligned 100-row
  blocks to be measured beside N rows either side.

2026-10-11: Measured aligned blocks beside N rows either side, as the user asked. The record is
[`verification/2026-10-11-linux-remote-read-ahead-blocks`](../../../../verification/2026-10-11-linux-remote-read-ahead-blocks/README.md).
The prototype is on `lab/remote-read-ahead` at `21d7a94`, never merged. The setup was the first record's:
- the same harness, pace and three layouts, at 150 ms a round trip, in Playwright's bundled Chromium on a Linux
  container;
- blocks of 50, 100 and 200 rows: the aligned blocks covering the range and 10 rows either side, as ag-grid's
  `cacheBlockSize` and `rowBuffer` ask;
- N = 30, 60 and 120 again, N = 60 as the anchor;
- b100 and N = 60 asked before the Window runs out ("t10"): once the rows on screen and 10 either side are not all in
  it, as ag-grid's rendered rows ask.

What it found:
- **Over one contiguous Window, blocks are N rows either side by another name.** Their Placeholders fall where their
  Window's size puts them. Held, the wheel and ArrowDown showed them 15% and 6% of the time at b100 over the narrow
  report, between N = 60's 19% and 8% and N = 120's 10% and 4%. b200's 400 rows did no better than N = 120's 255.
- **Per row scrolled, blocks carry a little less, in fewer and larger questions.** They answer 2.00 rows for each row
  scrolled, against 2.21 at N = 60. A block cache would halve that, as ag-grid's does. ExPivot's report source keeps
  one contiguous Window, so ag-grid's "every block kept" is not reproduced.
- **Asking before the Window runs out is what removes the Placeholders, blocks or not.**
  - One input at a time, the t10s met none, and held ArrowDown none.
  - The held wheel fell from 15% to 4% at b100 and from 19% to 8% at N = 60 (narrow).
  - Held PageDown, 11 to 13 rows a press, gained little.
  - It needs ExGrid to raise its Range Request before the rows on screen leave its Window, against ADR-0001. That
    would be a decision of its own.
- **A live update and the kept Windows grow with the Window's rows, whatever its shape.** Over the wide report the
  server projected b100's 200 rows in 138 ms an update and N = 60's 133 in 90 ms. Their kept Windows held 1.1 and
  0.7 MiB.
- **N = 60 again matched the first record where the pace did**: held 19% and 8% over the narrow report, against 19%
  and 7%.

The default is still the user's to decide, and the early ask beside it is a separate question.

2026-10-11, decided with the user after the second record
(`verification/2026-10-11-linux-remote-read-ahead-blocks/`): **60 rows either side by default**, as
`readAheadRows` on `PivotReportSource.Fetch`, and not blocks. Over one contiguous Window, blocks bought
nothing that N either side of the same size did not. The decision is
[ADR-0182](../../../adr/0182-a-remote-reports-window-is-read-ahead-sixty-rows-either-side.md), and
the criterion is LV-34. The Status is `ready-for-agent` for the build: the boxes left above.

Asking early, 10 rows before the Window's edge as ag-grid does, gave the largest gain. It was adopted
as its own decision,
[ADR-0183](../../../adr/0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md), and
is built by ticket 15. ExPivot's use of it needs this ticket's read-ahead first.

The lab branch `lab/remote-read-ahead` (21d7a94, on origin, never merged) holds the prototypes both
records measured.

2026-10-11: Built on `claude/remote-read-ahead`, test-first, with ticket 15's ExPivot half. Every box is
ticked but layer 3's: the spec is written and has not run yet.
- **The number is the source's.** `PivotReportSource.ReadAheadRows` is virtual and 0 by default;
  `LocalPivotReportSource` declares 0; `PivotReportSource.Fetch(..., readAheadRows: 60)` takes it as its
  last argument, refusing a negative one by name, and `FetchingPivotReportSource.ReadAheadRows` says it.
- **ExPivot widens and keeps.** `WindowNeededAsync` asks for the range the grid needs widened by the
  source's read-ahead, clamped into the report, and the first Window is 64 rows as before. A question in
  flight whose Window covers the rows on screen stays in flight. With nothing in flight, a range the
  Window held covers, with the grid's margin either side, asks nothing.
- **An answer short of the rows the grid needs is refused by name.** The report protocol already
  required every answer to be the Window asked for (ADR-0152). So a short answer was refused, but as
  "another request or Window" or "a different extent". The refusals now name the rows asked and the
  rows answered, and cite ADR-0182. This is stricter than ADR-0025, which accepts an answer short of
  the read-ahead that still reaches the rows the grid needs. Accepting one here would change ADR-0152's
  rule that an answer is the Window asked for, and nothing in ADR-0182 asks for that.
- **Window Changes over the widened Window** are pinned: a deal ten rows into the read-ahead, out of
  sight, changes, and its row arrives as a Window Change against the whole Window, with its digest.
  Scrolled to, it is painted from the Window held, and nothing is asked.
- **Tests:** `ReportReadAheadTests` (layer 2, 20 cases), two refusals in `ReportProtocolTests`
  (layer 1). `ReportShrinkTests`' transports now pass `readAheadRows: 0`, so their Windows are still
  where the grid stands, as ADR-0151's clamp is stated there. A shrink under a Window read ahead is
  pinned in `ReportReadAheadTests`.
- **Layer 3, written and not run yet:** `pivot-db.spec.mjs`, "LV-34/FN-3a/ADR-0182/0183: a server report
  scrolls within the Window it read ahead asking nothing, and asks once ten rows before it runs out".
  It counts the page's tally of Window questions, and on WebAssembly reads the Window each question
  names from the request.
- **Docs Site:** the Sources page says how far a server's report is read ahead, that it is asked
  early, and how an answer is checked. The server Example passes `readAheadRows: 60`.
