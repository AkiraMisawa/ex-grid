# 14: A remote report's Window is read ahead

Status: needs-triage

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

- [ ] The measurement is recorded, and the user has decided
- [ ] The decision is in an ADR, and the Definition of Done says what holds
- [ ] Built, and each of these holds:
  - a scroll within the read-ahead asks nothing;
  - a scroll past it asks once, for the widened Window clamped into the report;
  - an answer that does not reach the rows the grid needs is refused, as ADR-0025 refuses one;
  - Window Changes and their Window Digest cover the whole widened Window
- [ ] Layers 1 and 2 are green
- [ ] Layer 3 counts the report source's Window questions while it scrolls, and never reads a time.
  `/pivot-db`'s status line already counts the questions of each kind.
- [ ] The Docs Site's ExPivot Sources page says it, and its server Example passes the number

## Comments

2026-10-10: Written up with the user after PR #70's review. It started from two questions. Does
scrolling a server's report recompute it? It does not. Would reading ahead serve the user better?
