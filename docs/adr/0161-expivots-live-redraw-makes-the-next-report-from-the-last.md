# ExPivot's live redraw makes the next report from the last

*(Decided with the user on 2026-10-07, in the grilling of live data continued — Q2, Q9 to Q14, and the
marks of a redraw laid out afresh, asked while this was written. It answers ticket 03 of
`docs/specs/live-data`, and settles the reservation in §21.11, "ExPivot keeping the report rows a
redraw did not change". The measurements are ticket 01's,
[`2026-10-06-macos-live-update-costs-cc`](../../verification/2026-10-06-macos-live-update-costs-cc/README.md).)*

ExPivot builds its cube, its report and every report row again on every live redraw, at most four times a
second. Ticket 01 measured where that time goes. CoreCLR figures are the least of 15 runs; browser figures
are published WebAssembly, headless, Apply to the painted frame.

| Report rows | Whole redraw, CoreCLR | Building the report | Making the cube | Folding the batch | Browser |
|---:|---:|---:|---:|---:|---:|
| 10,001 | 3.5 ms | 37% | about 30% | under 1% | 57 ms |
| 101,001 | 60 ms | 53% | about 30% | under 1% | 529 ms |
| 401,001 | 269 ms | 59% | about 30% | under 1% | about 2,300 ms |

- **What else costs.** The grid's check of the keys is 3–4% of a redraw, and `Show` is next to nothing.
  All 11 painted rows render on every redraw, and 8 to 11 of them had no painted change.
- **Each redraw is a whole new generation.** It is 52 MB at 101,001 rows and 194 MB at 401,001 in
  WebAssembly
  ([`2026-10-06-macos-pivot-oom`](../../verification/2026-10-06-macos-pivot-oom/README.md)). The Change
  Highlight's history keeps about five of them with the default settings.
- **Reclaiming each generation costs CoreCLR's collector** 13–28 ms a redraw at the median at 101,001 rows,
  in steady state
  ([`2026-10-06-macos-paint-text-cost`](../../verification/2026-10-06-macos-paint-text-cost/README.md),
  H3).

ag-grid keeps a group's node by its id across updates and aggregates again only along the changed path
([`groupStrategy.ts#L492-L494`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L492-L494),
[`#L523`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L523)).
Its pivot mode buckets every row again on every refresh
([the research note](../research/ag-grid-rendering-on-data-change.md), §2.4). So the family's fold
([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)) is
already ahead of it. What ag-grid has that ExPivot lacks is that a node and its rendered row outlive an
update.

## The engine makes the next cube and report from the last

- **Under the same layout, a data redraw starts from the cube and report on screen and the leaves that
  changed.**
  - The axis nodes are kept.
  - The cells on a changed path are computed again: a changed leaf's cell, every subtotal above it on
    both axes, and the grand totals.
  - A report row whose path did not change is the same row in the next report.
- **Nothing is rewritten in place** ([ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md);
  principle 2).
  - The next cube shares the axis trees and takes its own copy of the values that change.
  - The next report shares the rows it did not change.
  - The cube and the report on screen stay exactly as they were, and remain correct for anything still
    reading them.
- **The result is the same as building afresh**, which the property test below holds it to.

## Which leaves changed

- **An answer may say which of its leaves changed since an earlier Source Version**, and the engine
  names the version it holds. The bundled `SnapshotPivotSource` knows them: they are the leaves its fold
  touched.
- **When an answer does not say, the engine compares the new answer's leaves with the previous answer's.**
  A server's source that says nothing is still answered correctly. What the comparison costs at 400,000
  leaves is measured when it is built.
- **[ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)'s
  answer gains the changed leaves as an optional part.** No source has to supply it.

## When a redraw starts afresh

Some redraws are built afresh, as every redraw is today, with every row a new instance. The result is the
same either way, so the line is drawn where sharing would be unsafe or would not pay:

- **A change that is not data**: a layout, a sort, a collapse, a form, the words or the culture.
- **A change of structure**: an Item appears or leaves, so the Row Sequence Version moves
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
- **A batch the source could not fold**: a compaction, `MaxLeaves`, a cap, or a source handed in afresh
  (ADR-0066/0067).
- **An order that follows values**: a sort by a Value Field, or a Top N filter, where a changed value can
  move a row among its siblings.
- **A Show Values As that reads other rows**: % of a total or of a parent, difference from, running
  total, rank, index.
  - Under these, a change on one path moves the shown values of rows on other paths.
  - A shared row would keep painting its old value, because its instance did not change (ADR-0003). That
    is quiet wrongness, so these redraws share nothing.
- **So many leaves changed that building afresh is cheaper.** That is the engine's choice, as the
  incremental requery's is in [ADR-0141](./0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md).

The first version shares only in the common case: the values of existing leaves changed, with the
structure, the order and every shown value's dependencies inside its own path. Sharing across a change of
structure is left for later.

## What a report row is

- **A report row says what it stands for, and holds no value and no report.**
  - It holds its role, its Value Field, its Items (which make its Row Key), and its labels.
  - A value cell is asked of a report: `report.ValueAt(row, column)`. The report computes it when it is
    first read, and keeps it.
- **So a row can belong to every report it did not change in**, without holding any of them alive
  ([ADR-0160](./0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)).
- **`PivotReport.cs:239`'s contract**, "a new report is new rows", becomes "a next report shares the rows it
  did not change".
- **ExPivot's checks that a row is the report on screen's** — its context commands and Show Details —
  compare the row's key with the report on screen. They no longer compare the row's report by reference.
- **Reading values costs what it costs today**: a lookup and a computation on first read, painted cells
  only. A shared row's cells are read afresh from each report it is painted under, which is the painted
  rows' cells at most.

## The Change Highlight keeps times, not reports

- **ExPivot hands the grid one `CellChangedAt` delegate for as long as a history lasts.**
  - A history starts on a layout, a sort, a collapse, a form or new words, as today, and only data
    extends it.
  - A row then renders when its instance is new, not because the delegate is
    ([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)). That
    is how the bundled sources already do it (LV-9).
- **The history holds the time each cell's painted text last changed, by row key and value column**, as
  `GridSource.From` does. It holds no report.
- **The times are set when a data redraw is laid out:**
  - the rows on a changed path are compared with the previous report's painted text;
  - a row new to the report is marked whole;
  - a redraw laid out afresh under data compares every row, in slices (PV-40), because rows off the
    changed paths can have changed too.
  - The previous report is held only while the next is built, then let go.
- **The marks follow ADR-0067's rules unchanged.**
  - A cell is marked when its painted text differs from its previous version's.
  - A change the format hides is not marked.
  - A sort or a filter marks nothing.
- **A mark ends with its time.** An entry goes when its time is over or its row leaves the report. Today's
  history kept its reports until the next data version came, so a burst left them in memory after the
  feed went quiet.
- **Considered while writing this, and not taken:**
  - keeping the earlier reports for a redraw laid out afresh, as today (about five full reports, which at
    401,001 rows risks the browser's heap again);
  - marking only the changed paths there, which would change ADR-0067's rule and leave changed cells
    unmarked.

## A redraw that runs out of memory

- **An `OutOfMemoryException` while the cube or the report is made is caught at the redraw.**
  - What was being built is dropped.
  - The report on screen stays, and is marked a Stale Report with the reason (ADR-0067).
  - The next change asks again.
- **The reason says memory ran out, not that the data is wrong.** A WebAssembly heap does not give memory
  back, so a redraw that failed may fail again.
- **Today the exception is unhandled and the page stops.** That contradicts principle 1: say it cannot be
  done.

## How it is checked

- **A property test (Layer 1), as PV-34 and LV-5 have.**
  - Over random batches and layouts — subtotals on and off, each form, values on rows and on columns,
    each Aggregation and Show Values As — the next report made from the last equals a report built
    afresh: row by row, key by key, cell by cell, in values and in painted text.
  - A row whose painted text changed is a new instance, and a row on an untouched path is the same
    instance.
  - The seed is recorded.
- **Rows rendered per redraw (Layer 2, M3's count).** On `/pivot-live`'s generator, the rows that render
  per live redraw are exactly the painted rows whose painted text changed.
- **Memory (Layer 2).** After a run of live redraws, no report but the one on screen is alive (ADR-0160).
- **Out of memory (Layer 2).** A redraw that throws `OutOfMemoryException` leaves the report on screen,
  stale, with the reason.
- **Observational.** Ticket 01's ExPivot measurement repeated, before and after, on CoreCLR and in the
  browser, at 10⁴, 10⁵ and 4×10⁵ report rows, with the collector's pauses. PV-21 recorded beside it.

## Considered options

- **Keep the instances of unchanged rows after a fresh build** (ticket 03's 1b; the research note's
  candidate 1). Rejected as the way: it saves under 1 ms of row renders on CoreCLR, and none of the build
  or the memory. Under this ADR the same rows are shared anyway.
- **A row that keeps its own values**, computed when the row is made. Rejected: every row's every cell
  would be computed on every build, where today only painted cells are.
- **A row that keeps pointing at the report it was made in.** Rejected: shared rows would keep old
  generations alive, the leak found on 2026-10-06 in another place.
- **Only the source says which leaves changed**, or **only the engine compares**. Rejected for both
  together: the bundled source knows and should say, and a source that does not know must still be
  answered.

## Consequences

- **The engine's public surface changes.** `PivotReportRow.ValueAt` and `PivotReportRow.Report` move to
  the report (`PivotReport.ValueAt(row, column)`), and `PivotAnswer` gains its optional changed leaves.
  The packages are prereleases (ADR-0042). *(As built, 2026-10-07: a source sees
  `PivotQuery.ChangedSince`, and answers with `PivotAnswer.ChangedLeaves` through `WithChangedLeaves` and
  `PivotLeafChanges`; the next cube and report, and the comparison, stay internal to the engine and
  ExPivot. The engine starts afresh when more than a quarter of the leaves, and more than 16, changed.
  Measured on CoreCLR at 400,000 leaves with one leaf changed: the next cube in 7.6 ms with the leaf
  named, 12.2 ms with the engine comparing, against 36.5 ms built afresh.)*
- **ADR-0060, ADR-0066, ADR-0067 and ADR-0068 are noted.**
- **ExPivot vouches for its report** (ticket 02, ADR-0141 as amended on 2026-10-07).
- **§21.11's reservation of kept report rows is settled here**, and the Definition of Done gains PV-44 to
  PV-48 (§29).
