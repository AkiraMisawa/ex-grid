# Live data: a Change Batch makes the next Snapshot, and ExPivot folds it in

*(Numbered ADR-0066 until 2026-10-01. ExSheet's Pointing Scope took ADR-0058 first, and ExPivot's
ADRs moved up by one into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for them. Commit messages before then use the old numbers.)*

*(Decided with the user, 2026-09-30, in the ExPivot grilling — Q41 to Q45, Q56 to Q58, Q60 and Q61.
The user brought it into the first version: "we will need it soon, and it is not only ExPivot's".
The Snapshot's side is in [ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md);
this ADR is what a pivot does with it.)*

Data that changes while it is being read is the ordinary case for this family. A blotter or a risk
screen receives new and changed trades every few seconds. The data changes in one of two ways,
depending on where it lives:

- **in the process**: the Consumer applies a **Change Batch** to the Snapshot the bundled Pivot
  Source holds;
- **on a server**: the Consumer learns that the server's data moved on.

What reaches the screen is the same either way.

## The bundled source folds a batch in

`PivotSource.From(snapshot)` takes a Change Batch, makes the next Snapshot from it, and brings the
answer it holds for the current question up to date **from what the batch removed and added**. It
does not start again from a million records.

- **Exact parts are updated by subtraction and addition**: the count, the count of numbers, and the
  sum of an Integer or a Decimal column. Integer arithmetic is exact, so the order of the updates
  cannot matter.
- **Every other part is recomputed from the records, for each leaf the batch touched.** That covers
  a Double's sum, the extremes, the product and the variance.
  - Subtracting from a floating-point sum drifts.
  - A removed maximum has no known successor without the records.
- **The invariant, and its test:** after any sequence of batches, every leaf equals a fresh
  aggregation of the Snapshot those batches made, to the last bit.
- **Leaves come and go with their Items.** An Item that first appears in a batch brings its leaves.
  An Item with no record left takes its leaves away.
- **Target, observed and never gated:** 1,000 changes to a million records reach the screen within
  0.2 s in the browser (Definition of Done, observational). *Observed 2026-10-01: 43 ms (35–57)
  from `Apply` to the frame that shows them, in a published WebAssembly build over a million
  trades, and 6.3 ms on CoreCLR (`verification/2026-10-01-linux-measure`).*

## A server's source says the data moved on

The Consumer tells its `PivotSource.Fetch` that the server's data changed, however the Consumer
learns it: SignalR, polling, or a message bus (Q57).

- **ExPivot asks again for the whole answer.** The Leaf Aggregates are the size of the report, not
  of the data
  ([ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)).
- **A server that sends only the leaves that changed comes later**, and only if a measurement shows
  that the whole answer is too slow.

## What the screen shows

- **Never half a batch** (Q45). A report is computed from one Snapshot: the one before a batch, or
  the one after it.
- **Changes are gathered, and the report is redrawn at most every 250 ms, from the newest version**
  (Q58). The Consumer may set the interval; 0 redraws on every change. A value that changes forty
  times a second cannot be read, and one that changes four times a second still feels live.
- **A change of values alone keeps the Selection** (Q45).
  - The Row Sequence Version moves only when the report's rows change: their keys, or their order
    ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md),
    [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)).
  - A menu or a panel that is open stays open.
  - An Item that appears or leaves changes the rows, so the Selection is dropped, as ADR-0011
    requires.
- **A cell whose shown value changed is marked** with a Change Highlight
  ([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)),
  under these rules (Q60):
  - **Only the data marks a cell**: a Change Batch, a server's change, or Refresh. A new layout, a
    sort or a collapse changes every cell at once and marks none.
  - **Every cell of a row that appears is marked.** A row that leaves simply goes.
  - **The comparison is of the painted text.** A change that the number format hides is not marked,
    so a mark never appears on a value that looks the same.
- **When the newest data cannot be shown, the report says so, and stays on the last version it could
  compute** (Q61). This is a **Stale Report**.
  - It arises when new data would make the layout break a cap (ADR-0066), or when the server fails
    or refuses.
  - The report stays on screen. Above it, ExPivot states what happened and the time of the version
    shown, and offers Retry.
  - Principle 1 refuses a plausible wrong answer. An old answer that is labelled as old, with its
    time and the reason, is not one. On a live risk screen, the last known values are worth keeping
    in view.

## Refined while building it

*(2026-10-01, when the bundled source's side was built.)*

- **A batch that compacts the Snapshot is not folded.** A compaction moves rows
  (ADR-0064), so the answer held for the current question is dropped and the next question is
  answered afresh. So is a batch whose new Items would pass `MaxLeaves`: the fresh question is
  refused, naming the bound, and ExPivot shows a Stale Report. Either way no report is computed
  from half a batch.
- **Subtraction is used only where no sum can round.** A leaf whose exact sum has passed 128 bits
  ([ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)) is
  a `double`, and is recomputed from its records whenever a batch touches it.

*(2026-10-01, when the component's side was built.)*

- **A question for newer data does not dim the report.** Dimming it four times a second would
  flicker. A Refresh, a Retry and a new source still show the loading indication, because the
  user asked for them.
- **A column that appears is marked too**, every cell of it, as a row that appears is.
- **A new source is data**: its changed cells are marked, and a refusal of its answer is a Stale
  Report. **New caps from the Consumer are not data**: they mark nothing, and a refusal goes to
  the Pivot Toolbar's notice, as a refused layout does.
- **A failed Refresh is a Stale Report.** The newest data could not be shown, and the notice
  says so with the time of the version on screen and Retry. A failed question for a layout the
  user asked for is not stale data: it goes to the Pivot Toolbar's notice, and the layout goes back.
  - **Retry after a failed Refresh refreshes again.** What failed was the refresh, and asking a
    source that never refreshed would show its old data as the newest.
  - **Before the first report, a failed Refresh goes to the Pivot Toolbar's notice**, because there
    is no report to be stale.
  - **The layout goes back only when a report is on screen.** When the first question fails, the
    pane keeps its layout, so the next change asks for it again.
- **The as-of time is the clock's**: when the answer on screen arrived, in the clock's local zone
  and the report's culture, with the date when it is not today. On Blazor Server, the clock's
  zone is the server's unless the Consumer hands ExPivot a `Clock` that answers the user's.
- **Gathering keeps redraws apart whatever the latency.** A change is asked for no sooner than
  the interval after the last change reached the screen, whether that change was shown or found
  unshowable. A user's gesture supersedes a question for newer data, and its own question brings
  the change; a data change never cancels the user's question.

## Considered options

- **Only additions** (Q42, option a) — rejected: a blotter's trades are amended and cancelled.
- **Partitions replaced whole**, such as "today's trades" (Q42, option c) — covered, because it is a
  batch that removes by key and adds.
- **The Consumer hands a whole new Snapshot every time, and the library finds the difference by
  key** (Q43, option b) — deferred. Finding the difference means reading every record, and it
  serves only an application whose only source is a periodic full reload. It is added if one asks.
- **A redraw on every change** (Q58, option b) — rejected, for the flicker described above.
- **Clearing the report when the newest data cannot be shown** (Q61, option b) — rejected, for the
  reason given above.

## Consequences

- **A layer-1 property test** applies random batches and compares every leaf with a fresh
  aggregation (§29).
- **The component coalesces**, keeps the Selection when only values change, marks cells, and shows
  a Stale Report. Each of these is a §29 criterion.
- **`/pivot-live` shows both ways**: a Change Batch fed to the bundled source, and a server's
  source whose data the demo's API server keeps changing
  ([ADR-0069](./0069-the-demo-pages-call-a-demo-api-server-both-hosts-share.md)).
- **ExGrid and ExSheet take live data up in ADRs of their own** (ADR-0064). A plain ExGrid can
  already show a live Window and mark its changes (ADR-0068, `/grid-live`).
- *(2026-10-05.)* **ExGrid's is [ADR-0141](./0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md).** It adopts Q43's option b — a whole new
  list paired by key — for `GridSource.From`, where pairing compares references and reads no
  values. ExPivot's deferral stands.
