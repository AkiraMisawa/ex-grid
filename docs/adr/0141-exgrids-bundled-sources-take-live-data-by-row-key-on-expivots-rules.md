# ExGrid's bundled sources take live data by Row Key, on ExPivot's rules

*(Decided with the user, 2026-10-05, in the grilling of ExGrid's live data — Q1 to Q4, Q4', Q6 and
R2 to R7.
[ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md) and
[ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)
announced it: "ExGrid and ExSheet take live data up in ADRs of their own". This is ExGrid's. The
research note is [here](../research/ag-grid-rendering-on-data-change.md), and the measurements are in
[`verification/2026-10-05-macos-live-update-measure`](../../verification/2026-10-05-macos-live-update-measure/README.md).)*

A blotter receives new, changed and cancelled trades all day. Today a Consumer that shows them in
ExGrid writes the hard part itself. The `/grid-live` demo does all of this by hand:

- it pairs trades by `TradeId`;
- it replaces each changed trade with a new instance at its position;
- it bumps the Row Sequence Version when a position names another trade;
- it compares old and new values for the Change Highlight;
- it gathers notices.

That is what ag-grid does for its users when `rowData` is replaced under `getRowId`.

**Written by hand, it goes wrong quietly.** `/grid-live` asks the server only about trades in the
Window or before it (`GridLivePage.razor:330`).

- A trade cancelled after the Window moves every later row up one.
- When a Selection reaches past the Window, its positions then name other trades.
- The Row Sequence Version does not move, so the Selection is not dropped.

**`GridSource.From`, the bundled in-memory source, takes one change at a time.** `ReplaceRow`
re-runs the whole query. At 10⁶ rows that is 354 ms per change, and about six minutes for 1,000
changes (derived from timed single calls; M4).

**The bundled sources now take live data themselves**, in both of the ways ADR-0067 names. What
reaches the screen is the same either way.

## Rows stay the Consumer's objects

- **The rows a source hands the grid are the Consumer's objects, named by a Row Key**
  ([ADR-0140](./0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md)).
  Typed columns and templates work as they do today.
- **The Snapshot is not adopted here.**
  - ADR-0064's rule stands: a Snapshot serves a grid "as an index beside those objects, never as
    their replacement", and ExGrid adopts it after measuring.
  - The measurements did not call for it. With the incremental requery below, 1,000 changes to 10⁶
    objects take 3.2 ms.
- **The Snapshot is reserved with two triggers** (Definition of Done §21.11):
  - a screen that shows the same live data in ExGrid and ExPivot. Under this ADR it applies each
    change twice: once as rows to the grid's source, once as a Change Batch to the pivot's.
  - a measurement showing that a user's sort of 10⁶ rows is too slow in the browser. A full requery
    takes 365 ms on CoreCLR; the browser was not measured.

## In the process: `GridSource.From` with a Row Key

Given a key, `GridSource.From` takes live data. Without a key, it is the source it is today.

- **A Change Batch** is applied as one: the rows added, the rows changed (found by their key) and
  the keys removed.
  - A key that repeats, or a changed or removed key the source does not hold, refuses the whole batch
    by name.
  - The screen never shows half a batch.
- **A whole new list** is taken too, as ag-grid takes `rowData` under `getRowId`. Rows are paired by
  key:
  - a new key is an added row, and a missing key is a removed one;
  - a different instance under a key is a changed row, and the same instance is an unchanged one.
  - ADR-0067 deferred this for ExPivot (Q43, option b), because finding the difference reads every
    record. Here it compares references, not values, so the deferral does not apply.
  - A Consumer that builds every object afresh on each reload gets every row as changed. That costs
    more, and it is not wrong: marks compare painted text (below).
- **The requery is incremental.**
  - The changed and added rows are tested against the Filter, sorted alone with their original
    position breaking ties, and merged into the previous order. Removed rows, and the old versions
    of changed rows, leave it.
  - **Its result equals `GridQueryEngine.Apply` exactly**, the stable tie order included
    ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)). A property
    test gates it. The prototype was equal over 108,000 random batches (M5).
  - It wins when few rows change. At 10⁶ rows it took 0.23 ms for one change and 3.2 ms for 1,000,
    against 365 ms for a full requery.
  - It loses when many rows change. At 10⁵ rows it took 59.6 ms against 31.3 ms for 50,000 changes.
    Where the source switches to a full requery is its own choice, because the result is the same
    either way.
- **A user's own edit is not gathered.** `ReplaceRow` still applies at once.

## On a server: `GridSource.Fetch` hears that the data moved on

- **The Consumer tells the source that the data moved on**, however it learns it: SignalR,
  polling or a message bus, as in ADR-0067's Q57.
- **The source reads the Window again.** It pairs the new rows with the painted ones by Row Key, and
  marks the cells whose painted text changed. `GridSource.Fetch` therefore needs a Row Key for live
  data. The Row Mark adapter's key is one.
- **Only the server knows whether the order of the whole result moved.**
  - An answer (`GridPage`) may carry the server's order token. The Row Sequence Version moves when
    the token differs.
  - A server that sends no token is taken to have moved the order with every change.
  - Comparing the Window alone would leave the hole `/grid-live` has.
- **`/grid-live` moves onto this path**, which closes its hole.

## What the screen shows

These are ADR-0067's rules, so that an ExGrid and an ExPivot over one feed move together.

- **Changes are gathered, and the screen is redrawn at most every 250 ms, from the newest
  version.**
  - The Consumer may set the interval. 0 redraws on every change.
  - The first change after a quiet interval is shown at once. ag-grid's `applyTransactionAsync`
    instead waits 50 ms from the first change, every time.
  - The clock is the source's, a `TimeProvider` that a test drives. Gathering belongs to a source;
    the grid's core has no clock for data.
  - Gathering bounds the cost. A redraw paints at most the rows in view, at most four times a
    second, however fast the changes arrive.
- **The order follows at once.** The Selection is dropped whenever the order of the result moved
  anywhere, as today ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
  - **Dropping it only where the move meets the Selection** was considered (Q4'). The user rejected
    it: "If the order of the keys changed, dropping the Selection is good."
  - **Holding the order while only values change** was considered and not taken. ag-grid offers
    this as `suppressModelUpdateAfterUpdateTransaction`. A held order would have to be shown. Excel
    shows a sort arrow over rows that are no longer in that order.
  - **ag-grid keeps a cell range where it was through a sort or new data.** Its range service
    registers no listener for either, so the range then names other rows. ADR-0011 rejected exactly
    that.
- **The Change Highlight is the source's**
  ([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)),
  by ADR-0067's rules:
  - a cell is marked when a changed row's painted text differs from its previous version's;
  - every cell of a row that appears is marked;
  - a sort or a filter marks nothing;
  - a change the format hides is not marked.
- **A write over live data** follows
  [ADR-0142](./0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md).

## The grid's pass over a new Window

- **Every new Window is checked for a row that appears twice** (`ExGrid.razor:2691`, `:2858`).
  - The check is a pass over the whole Window, which under `GridSource.From` is the whole result:
    18.1 ms at 451,115 rows.
  - With the incremental requery, this pass becomes the largest cost of an update.
- **A bundled source that has refused repeated Row Keys vouches for its Window**, and the grid does
  not check a Window that is vouched for.
  - Two rows under different keys cannot be one instance, so the refusal is not weakened. It moves
    to the source, and it judges by key, which is stricter.
- **A Window that is not vouched for**, such as a Consumer's own, is checked as today.

## Settled while building it

*(2026-10-06, decided with the user — D5 to D9 — when the first build was put together.)*

- **A source puts out what it has gathered when the grid asks, before a write is judged** (D5;
  [ADR-0142](./0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md)).
  - `GridSource.From` publishes its gathered changes at once, so the write is judged against them,
    and its `ReplaceRow` takes an edit built on the newest version.
  - `GridSource.Fetch` has nothing to put out: what it waits for is an answer from the server. A
    write under it is judged against what was painted, and whether the server's data moved under
    the write is the Consumer's server's to judge.
- **Under `GridSource.Fetch`, the Consumer may name the keys it knows were added** (D6).
  - The source holds only the Window, so a key new to the Window may be a record just added or one
    that slid into the Window from outside it. Only the server knows which.
  - When the Consumer names the added keys as it says the data moved on, a named key is marked
    whole, and a key that was not named is not: it slid in.
  - A named key the source had painted before is a row removed and added again, and is compared
    cell by cell, as `GridSource.From` treats a key removed and added again within one gathering.
    *(Refined while building it, 2026-10-06; the code review asked that it be written here.)*
  - When it names none, the source guesses. A new key is marked whole only where it cannot have slid
    in: between two rows painted before, or at an end of the result both Windows reached. Under a
    server's sort, a row that a change of value moved into the middle is then marked whole too.
  - ag-grid does not guess either: under its Server-Side Row Model, the application states which
    rows were added, updated and removed (`applyServerSideTransaction`).
- **A whole new list sets the order** (D7). Rows are paired by key as above, and the source's own
  order follows the list, as ag-grid's does under `getRowId`. The Row Sequence Version moves when the
  sequence moved. A reload whose rows come back in a new order is shown in that order.
- **The in-process path has a demo page of its own** (D8), with 10⁶ rows and Change Batches in the
  browser, where LV-15 is observed. A second grid on `/grid-live` would have broken that page's
  checks.
- **Judgements made while building it** (D9):
  - **The source's clock is `TimeProvider.System` by default.** A source cannot see the host's
    services, as a grid can; a test or a host hands it its own.
  - **A source keeps a cell's change time for `ChangeTimesKeptFor`**, one minute by default. It must
    be at least the grid's `ChangeHighlightDuration`, or a mark would end early. The times of a
    removed row are let go at once.
  - **Reading the Window again after a change does not raise `IsLoading`.** Dimming the grid four
    times a second would flicker; this is ExPivot's rule.
  - **The demo server counts a reset as a move of the order.**

## Considered options

- **Row views over a Snapshot.** Not now, for the reasons above.
- **Apply each batch as it comes.** Rejected for the reason ADR-0067 gave (Q58): a value changing
  forty times a second cannot be read.
- **A source for live data beside `GridSource.From`.** Rejected. A Consumer adds a key to the
  source it already uses.
- **Inferring the order from the Window on a server.** Rejected: that is `/grid-live`'s hole.

## Consequences

- **`GridSource.From` stays the reference implementation of Filter and Sort**
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md), ADR-0023). Its incremental
  path is proven equal to it.
- **ExPivot reusing the rows a redraw did not change is reserved** (Definition of Done §21.11). It
  is decided once ADR-0140 is built and measured in the real grid.
  - On `/pivot-live`'s generator, a median of 5 of 11 painted rows were unchanged per redraw with
    subtotals, 8 without, and none at 1,000 changes a batch (M3).
  - ExPivot's `CellChangedAt` delegate is new for each data version (`ExPivot.Report.cs:443-446`),
    and a row compares it by reference (`ExGridRow.razor:320`). A kept row would therefore still
    render.
- **ExSheet's live data** remains the subject of its own ADR.
- **The Definition of Done gains LV-3 to LV-10, LV-16 and LV-18** (§31).
