# How ag-grid repaints when its data changes, and what ExGrid and ExPivot could take from it

*Research note, 2026-10-05. It decides nothing: no ADR, no `CONTEXT.md` entry and no criterion of
the Definition of Done is changed by it. Where adopting something needs a decision, the decision is
written as a proposal in [§9, "Open questions for the user"](#9-open-questions-for-the-user).*

*(Later the same day, the predictions were measured — M1 to M5, in
[`verification/2026-10-05-macos-live-update-measure`](../../verification/2026-10-05-macos-live-update-measure/README.md) —
and the user decided §9's questions in a grilling. The decisions are
[ADR-0140](../adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md),
[ADR-0141](../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md) and
[ADR-0142](../adr/0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md). Where this
note and those ADRs differ, the ADRs hold. Candidate 1 is reserved, and candidates 2 to 5 are
decided.)*

- **ag-grid is pinned to release 36.2.0**: the `latest` dist-tag of `ag-grid-community` on npm
  (published 2026-09-16), git tag `release-36.2.0`, commit
  `0fee5b7b1e839ae23fe860e404042448f3c1375d`. Every ag-grid code link below is a permalink to that
  commit. Documentation is cited both as the published page on ag-grid.com and as its source file at
  the same commit, with line numbers.
- **This repository was read at `60d522ed`.** Repository citations are `path:line` at that commit.
- **Vocabulary.** ag-grid's API names (`applyTransaction`, `deltaSort`, `enableCellChangeFlash`,
  `getRowId`) are quoted as ag-grid's own and are not the family's words. For the family, this note
  uses `CONTEXT.md`'s terms: a **Change Batch** (not a delta, diff or transaction), a **Change
  Highlight** (not a flash), **Row Identity** (the grid's test for sameness, which is a reference),
  the **Record Key** (the declared column that tells a Snapshot's records apart), and the **Window**
  (the block of rows the Consumer pushes). "Blazor's diff" is the framework's render-tree diff
  (`RenderTreeDiffBuilder`), named as the framework names it; it is neither an Overlay nor a Change
  Batch.
- **Any statement about ExGrid's performance is a prediction** unless it quotes a recorded
  measurement. Each prediction says what would settle it (CLAUDE.md, "Measure before claiming
  anything about performance"). No benchmark, build or browser run was made for this note.

---

## 1. Summary

**The question:** how does ag-grid update what it draws when its data changes, and how could ExGrid
and ExPivot adopt that?

**ag-grid works in three stages.**

1. **It pairs the new data with the old by row id, and calls a row changed when its object
   reference differs.**
   - A new `rowData` array with `getRowId` makes the grid work out the adds, updates and removes
     itself and apply them as an internal transaction ([ag-nm-immutable], [doc-rowdata L54-L97]).
   - `applyTransaction` takes them already sorted out ([ag-nm-tx]).
   - `applyTransactionAsync` queues them on a 50 ms timer and applies the queue as one
     ([ag-csrm-async]).
   - Without `getRowId`, a new `rowData` throws every row away, clears the selection and scrolls to
     the top ([ag-nm-new], [ag-rr-aftermodel]).
2. **It re-runs its client-side row model from the grouping stage** — group, filter, pivot,
   aggregate, filter aggregates, sort, flatten — handing every stage the rows that changed
   ([ag-csrm-switch], [ag-changedrownodes]).
   - On grouped data (Enterprise) the work is limited to the **changed path**: the groups holding a
     changed row, and their ancestors ([age-cpfactory], [age-group-delta]).
   - A flat grid still re-filters every row ([ag-csrm-flat], [ag-filterstage]). `deltaSort`, off by
     default, sorts only the touched rows and merges them into the previous order ([ag-deltasort]).
   - Pivot mode re-buckets every row on every refresh. When a new pivot key appears, it regenerates
     the result columns and re-aggregates everything ([age-pivot-bucket], [age-pivot-exec]).
3. **It keeps each rendered row's controller across the update, by row id** ([ag-rr-recycle],
   [ag-rr-createupdate]). It moves the row, and refreshes a cell only when the cell's value
   (compared with `===` or `colDef.equals`) or its formatted text changed ([ag-cellctrl-compare]).
   The DOM is written only there, and the cell is flashed if the column asks
   ([ag-cellctrl-refresh]). Rows entering or leaving the rendered range are created or destroyed;
   nothing else is.

**ExGrid already has ag-grid's rule for what a change is, but uses it for two jobs.**

- ADR-0003's rule — a changed row is a new instance, and an unchanged row is the same instance — is
  ag-grid's "immutable store" rule ([doc-rowdata L54-L69]).
- But ExGrid uses the instance both to decide **that** a row changed (`ShouldRender`) and to decide
  **which component paints it** (`@key` is the instance; `src/ExGrid/Components/ExGrid.razor:446`).
  A changed row's component is therefore disposed and a new one mounted, and the row's DOM is
  rebuilt (`tests/ExGrid.Components/ChangeHighlightTests.cs:227-246`). ag-grid holds a separate id,
  and refreshes the same row in place.
- Blazor's own diff would give ExGrid ag-grid's per-cell "write only what changed" if the component
  were kept: within a retained component it emits a text update only for text that differs
  ([bl-diff-text], [bl-diff-attr]).

**ExPivot is ahead of ag-grid's pivot mode in recomputing, and behind it in repainting.**

- It folds a Change Batch into the Leaf Aggregates — exact parts by subtraction and addition —
  rather than re-bucketing every record (ADR-0067; `src/ExPivot.Engine/SnapshotPivotSource.cs:231-254`).
- It never shows half a batch, and it marks cells by comparing painted text
  (`src/ExPivot/Components/ReportHistory.cs:7-21`).
- But each redraw lays out a new report whose rows are all new instances
  (`src/ExPivot.Engine/PivotReport.cs:236-240`), so every painted row remounts on every redraw — up
  to four times a second by default (ADR-0067).

**`GridSource.From` has no incremental path.** One `ReplaceRow` re-filters and re-sorts every row
(`src/ExGrid/InMemoryGridSource.cs:148`), and there is no way to replace many rows at once. The
`/grid-live` demo writes by hand what ag-grid's immutable `rowData` does for its users: pairing by
key, the Row Sequence Version, and which cells changed
(`samples/ExGrid.DemoPages/Pages/GridLivePage.razor:226-244`, `:321-387`).

**Ranked adoption candidates** (§8):

1. ExPivot keeps the instances of report rows whose painted values did not change.
2. An opt-in key that ExGrid uses only to keep a row's component, with Row Identity still the change
   signal.
3. A bundled live Grid Source over a Snapshot and Change Batches.
4. Incremental requery in the bundled sources: filter only the changed rows, and merge them into the
   previous order.
5. Grid-side work per update that grows with the Window.

**Already rejected by an ADR** (§7): row and cell animation (ADR-0027 P8, ADR-0068); the grid
comparing values (ADR-0068); a grid-held row model (ADR-0001); positional ranges that outlive a
reorder (ADR-0011); clearing the view when a cap is passed (ADR-0067); and the library finding the
difference between two whole arrays by key (deferred by ADR-0067).

---

## 2. ag-grid: from a change to a paint

### 2.1 How a change enters the grid

#### A new `rowData` without `getRowId`

- **Which path a new array takes.** The client-side row model treats a new `rowData` as a
  wholesale replacement whenever `getRowId` is absent, the grid holds no rows yet, the new array is
  empty, or `resetRowDataOnUpdate` is set ([ag-csrm-onprop], [ag-opt-getrowid]).
- **What a replacement destroys.** `setNewRowData` destroys every row node, resets the selection
  (`selectionSvc.reset('rowDataChanged')`) and the id counter, and builds new nodes ([ag-nm-new]).
- **What the view does.** The refresh carries `newData: true` and no `keepRenderedRows`. The
  RowRenderer then removes every row component, and scrolls to the top unless
  `suppressScrollOnNewData` is set ([ag-rr-aftermodel], [ag-opt-scroll]).
- **What the documentation says it costs.** Without row ids, the selection is lost, groups are
  re-created and closed, "all rows destroyed from the DOM and recreated, flicker may occur", and no
  cell can flash ([doc-rowdata L16-L24]).

#### A new `rowData` with `getRowId` — the "immutable store"

- **The documented rules** ([doc-rowdata L54-L69]). An item whose id is new is added. An item whose
  id exists but whose object reference differs is an update; for the same reference "it's assumed
  the data is the same as the already present data". A row whose id is missing is removed. The
  grid's unsorted order follows the list.
- **The code** ([ag-nm-immutable]).
  - Each item is looked up by `getRowId`. `node.data !== data` calls `updateData` and records the
    node in `updates`; an unknown id creates a node in `adds`.
  - `deleteUnusedNodes` destroys every node the new list did not name, records it in `removals`,
    and deselects it ([ag-nm-delete]).
  - A node whose source index moved up marks the order `reordered`;
    `suppressMaintainUnsortedOrder` skips keeping the list's order.
- **The refresh** asks to keep the rendered rows, with animation, and carries the changed rows
  ([ag-csrm-onprop]).
- **The documented costs** ([doc-rowdata L81-L97]). Once the grid has worked out what changed, "it
  then creates a transaction with these details and applies it". Working out what changed is an
  overhead, and there is no asynchronous form of it.
- **A duplicate id is a warning**: "Duplicate node id … this could cause issues in your grid"
  ([ag-nm-create], [ag-errtext]).

#### `applyTransaction`

- **Shape and order.** A transaction is `{ add, addIndex, update, remove }`, applied as remove, then
  update, then add ([ag-nm-tx]).
- **Finding the rows.** Rows to update or remove are found by `getRowId` through a map, or without
  it by `===` in a linear scan over every leaf ([ag-nm-lookup], [ag-nm-lookupref]). The
  documentation calls the reference form slower for thousands of rows ([doc-tx L33-L83]).
- **Order.** An `addIndex` inside the list marks the order `reordered` ([ag-nm-tx]).
- **The refresh.** The value cache is expired, and the model refreshes from the grouping stage with
  `keepRenderedRows: true` and the changed rows ([ag-csrm-update], [ag-valuecache]).

#### `applyTransactionAsync` and `asyncTransactionWaitMillis`

- **The queue** ([ag-csrm-async]).
  - The first call starts a `setTimeout` of `asyncTransactionWaitMillis`; later calls only append
    to the queue. The default wait is 50 ms ([ag-defaults], [ag-opt-async]).
  - When the timer fires, every queued transaction is applied into **one** set of changed rows and
    **one** model refresh.
  - The callbacks run in a later `setTimeout(0)`, and an `asyncTransactionsFlushed` event lists the
    results.
  - `flushAsyncTransactions` clears the timer and applies the queue at once.
- **The documentation's advice.**
  - Use it for "5 or more updates a second" ([doc-update L46-L62]).
  - A transaction "can take up to 50ms" to be applied.
  - To act on the latest data — "for example you may want to select a row in the grid but want to
    make sure the grid has all the latest row data" — flush first ([doc-hf L43-L51]).

#### `rowNode.setData`, `updateData` and `setDataValue`

- **`setData` and `updateData`.** Both replace the row's data, expire the value cache and dispatch
  `dataChanged`: with `update: false` for `setData`, `true` for `updateData` ([ag-rownode-setdata]).
  The row controller answers with a refresh of every cell, suppressing the flash and forcing new
  renderers when it was not an update ([ag-rowctrl-datachanged]).
- **`setDataValue`.** It writes one value and dispatches `cellChanged` ([ag-rownode-setdatavalue]).
  The change-detection service gathers the changed path, re-aggregates it, and refreshes the leaf
  row and the changed group rows ([ag-changedet]).
- **What none of them do.** They neither re-sort, re-filter nor regroup; the documentation points to
  `refreshClientSideRowModel()` or a transaction ([doc-single L22-L34]). The reason given is that
  rows jumping or vanishing while a user edits is bad experience ([doc-cd L149-L164]).

#### `refreshCells` and `redrawRows`

- **`refreshCells`** runs `refreshOrDestroyCell` on each rendered cell, with change detection unless
  `force` is set ([ag-rr-refreshcells], [doc-viewrefresh L9-L15]).
- **`redrawRows`** destroys and re-creates the named rows' controllers, or redraws everything when no
  rows are named ([ag-rr-redrawrows]). The documentation calls it "a much heavier operation", for
  properties read only when a row is created ([doc-viewrefresh L58-L66]).

### 2.2 What re-runs in the client-side row model

- **The pipeline.** A refresh names a starting step and falls through every later one: group,
  filter, pivot, aggregate, filter aggregates, sort, map ([ag-csrm-switch], [ag-csrm-stages]). The
  documentation gives the same order ([doc-tx L127-L144]). A new `rowData` and every transaction
  start at the grouping step ([ag-csrm-onprop], [ag-csrm-update]). `setData` and `setDataValue`
  start no refresh of the pipeline; `setDataValue` re-aggregates its changed path directly (§2.1).
- **What a stage is told.** The changed rows travel as one object: `reordered`, `removals`,
  `updates`, `adds` ([ag-changedrownodes]).
- **The changed path, which is Enterprise.**
  - It is created only for hierarchical (grouped or tree) grids, only for a refresh that carries
    changed rows, and never for new data ([age-cpfactory]). "Flat grids … never use changedPath"
    ([ag-csrm-flat]). Without the Enterprise module there is no factory, and "callers fall back to
    full (non-incremental) processing" ([age-cpfactory]).
  - Grouping adds the old parent of every removed or updated row, and the new parent of every moved
    or added row ([age-group-delta]). The later stages visit only those groups, deepest first
    ([ag-changedpath-walk]).
  - Aggregation recomputes each group on the path from **all** its children, reading a sub-group's
    already-computed aggregate ([age-agg], [age-agg-cols]).
    `aggregateOnlyChangedColumns` narrows a one-cell edit to the edited column
    ([age-agg-cols], [doc-cd L180-L202]).
  - The documentation's example counts 171 aggregations, about 24,000 comparisons and 10,000 filter
    passes at load, and "drastically fewer" for one update ([doc-tx L100-L118]). These are
    ag-grid's own figures, not measured here.
- **A flat grid.**
  - The filter stage tests every row on every refresh, and hands on the previous array when the
    result is identical, so that an unchanged result allocates nothing ([ag-filterstage]).
  - The sort stage sorts every filtered row, unless `deltaSort` is on and no `postSortRows` callback
    is set ([ag-sortstage], [ag-opt-delta]).
- **`deltaSort`** ([ag-deltasort], [doc-tx L146-L166]).
  - The touched rows are the updates, the adds and the rows on the changed path. They are sorted
    alone, with their current index breaking ties, and merged into the untouched rows' previous
    order.
  - The code states O(t log t + n) against a full sort's O(n log n). It falls back to a full sort
    when there is no previous order, or four rows or fewer in all.
  - The documentation warns that it can be slower when the transactions are large against the data,
    or the groups many and small.
- **`suppressModelUpdateAfterUpdateTransaction`.** An update-only transaction skips the pipeline, so
  that rows do not move under an edit ([ag-csrm-suppress], [doc-tx L127-L144]).
- **The value cache.** One global version, bumped on any data change; a row's cached values are read
  only when the versions match ([ag-valuecache]). It cannot be invalidated in part, because a value
  getter may read anything ([doc-vc L47-L56], [doc-vc L84-L91]).
- **What forces a full rebuild.**
  - New data without ids, or with `resetRowDataOnUpdate` ([ag-csrm-onprop]).
  - A change of grouping, which also turns animation off ([age-groupstage], [age-group-init]).
  - Pivot result columns that changed: the stage returns true and the changed path is dropped for
    the rest of the refresh ([ag-csrm-execute]).
  - A `postSortRows` callback, which turns `deltaSort` off ([ag-sortstage]).

### 2.3 How the view follows

- **The event chain.**
  - A refresh ends with `modelUpdated`, carrying `keepRenderedRows`, `animate` and `newData`
    ([ag-csrm-refresh]).
  - The page-bounds listener re-dispatches it as `paginationChanged` ([ag-pagebounds]), and the
    RowRenderer redraws ([ag-rr-pageloaded], [ag-rr-aftermodel]).
- **Which rows are kept.**
  - With `keepRenderedRows`, the rendered row controllers are re-indexed by row-node id
    ([ag-rr-recycle]).
  - For each position to draw, a controller already rendered for that id is reused; otherwise a new
    one is made ([ag-rr-createupdate]).
  - The controllers left over are destroyed. With animation, they are kept as "zombies" for 400 ms
    while they fade ([ag-rr-createupdate], [ag-rr-timeout]).
- **Moving a row is a style change.** A row is positioned by `translateY` (or `top`), so a moved
  row is not re-created ([ag-rowctrl-top]).
- **The DOM stays bounded.** The positions drawn are the rendered range — the viewport plus
  `rowBuffer` rows, 10 by default — plus the focused row, plus a row being edited or focused that is
  still in the model ([ag-rr-indexes], [ag-rr-unvirt], [ag-defaults]). The animation's zombies add
  rows for 400 ms.
- **When the work is done.** A data update renders synchronously. Animation frames are used only for
  rows created after a scroll ([ag-rr-createrow]), with a budget of 60 ms of tasks per frame
  ([ag-afs-frame]).
- **Which cells are told.**
  - An `updateData` reaches the row controller as `dataChanged`, and every cell of the row is asked
    to refresh ([ag-rowctrl-listen], [ag-normalrow-refresh]).
  - A `cellChanged` — `setDataValue`, or an aggregate that changed — reaches the cell of that column
    only ([ag-normalrow-listen], [ag-cellctrl-refresh]).
  - An aggregate fires `cellChanged` only for keys whose value changed, and only on a row with
    listeners, which is a rendered row ([age-aggdata]).
  - After every model update, cells that derive from an aggregate (a value getter, Show Values As)
    are refreshed in the rendered rows ([ag-rr-aftermodel]).
- **How a cell decides** ([ag-cellctrl-refresh], [ag-cellctrl-compare]).
  - It reads its value and formatted value again, and compares them with the last ones it painted:
    the value by `colDef.equals` or `===`, the formatted text by `!=`.
  - A column with no field, value getter or group display always refreshes, as does `force` or new
    data.
  - The column-definition and documentation side of the comparison: [ag-coldef-equals],
    [doc-cd L31-L68].
- **What a refresh writes** ([ag-cellctrl-show], [ag-cellcomp], [ag-cellcomp-refresh],
  [ag-cellcomp-text]).
  - A cell renderer's `refresh(params)` returning true keeps its instance.
  - False, no `refresh` method, or a different renderer class destroys it and creates another
    ([doc-renderer L21-L24]).
  - A cell with no renderer has its `textContent` replaced.
- **New data is not a change.** On `newData` the renderer is re-created and nothing flashes, because
  "we are showing stock price 'BBA' now and not 'SSD'" — a different thing, not a movement
  ([ag-cellctrl-refresh]).
- **Flashing** — Community, `HighlightChangesModule` ([ag-hcmodule]).
  - A cell flashes when `enableCellChangeFlash` is set, the refresh did not suppress it, and no
    filter change is being processed ([ag-cellctrl-refresh], [ag-coldef-flash]).
  - The flash service adds `ag-cell-data-changed` for `cellFlashDuration` (500 ms), then
    `ag-cell-data-changed-animation` with an inline CSS `transition` on `background-color` for
    `cellFadeDuration` (1,000 ms) ([ag-flash], [ag-defaults], [doc-flash L15-L56]).
  - One timer serves every cell, and ends that fall within 15 ms of each other are batched.
  - A flash belongs to the cell controller: when the cell is destroyed, its flash is dropped.
- **Row animation is on by default.** New rows fade in, old rows fade out, and moved rows slide
  ([doc-rowanim], [ag-defaults]).
- **ag-grid's React layer is the nearest analogue to Blazor.**
  - Rows are React children keyed by their controller's `instanceId` — the row id plus a sequence
    number, fixed when the controller is made ([agr-container], [ag-rowctrl-instance]) — so a
    recycled controller keeps its React component.
  - Rows already in the DOM keep their DOM order, unless DOM order is required, so a reorder is a
    change of transform and not a DOM move ([agr-order]).
- **Cell ranges are positions** (Enterprise).
  - A range's rows are `RowPosition`s: a row index and a pinned flag ([ag-cellrange], [ag-rowpos]).
  - In the code read, the range service clears ranges when pivot or row-group columns change, and
    registers no listener for a sort, a filter or new row data ([age-range]).

### 2.4 Pivot mode under updates (Enterprise)

- **Every refresh re-buckets every row.**
  - The pivot stage walks the whole tree from the root and buckets each leaf group's filtered
    children by pivot key ([age-pivot-bucket]). The changed path is used only to clear stale
    buckets.
  - The set of keys is compared with the last one by converting both to plain objects and comparing
    them as JSON ([age-pivot-unique]).
- **When the result columns are regenerated** ([age-pivot-exec]). When the keys, the value columns,
  their aggregation functions, the row-group columns, the strict column order or a pivot option
  changed, the stage:
  - makes new column definitions;
  - rebuilds the column tree, reusing the existing column objects by key and destroying the rest
    ([age-pivotcols], [age-pivotcols-apply]);
  - returns true, so the changed path is dropped and aggregation visits every group
    ([ag-csrm-execute]).
- **Otherwise aggregation follows the changed path.** A leaf group's value per pivot key comes from
  its bucket; a higher group's comes from its children's aggregates ([age-agg-pivot]).
- **The cap.** `pivotMaxGeneratedColumns` (−1, no limit, by default) stops the generation, sets the
  result columns to none and raises `pivotMaxColumnsExceeded` ([age-pivot-exec], [ag-opt-pivotmax],
  [ag-defaults]). The documentation: it "halts column generation, clears the view"
  ([doc-pivotcols L55-L62]).
- **The documented example** ([doc-cd L216-L238]). Adding a record with a new course adds a column
  "without touching the remaining columns or rows". In the code that is true of the column objects
  and the rendered rows; the aggregation runs over everything.
- **Cost, read from the code rather than measured.**
  - Every refresh is a pass over every row that passes the filter, plus the JSON comparison of the
    key tree.
  - A new key adds a rebuilt column tree and a full aggregation.

### 2.5 High-frequency updates

- **Batching.** The documented answer is `applyTransactionAsync`, its flush and its event (§2.1).
- **Costs the documentation names.**
  - Header checkbox selection checks every row on every update ([doc-tx L120-L125]).
  - Row animation ([doc-rowanim]).
  - Slow value getters, for which the value cache exists ([doc-vc L47-L56]).
  - Change detection itself, which `suppressChangeDetection` turns off ([doc-cd L70-L89]).
- **`suppressAnimationFrame`.** It makes rows created after a scroll render synchronously, and the
  documentation recommends against it ([ag-opt-scroll], [ag-afs]).
- **The Viewport Row Model** (Enterprise).
  - The server knows which rows are displayed and pushes changes to them ([doc-viewport L1-L36]).
  - `setDataValue` refreshes one cell in place; `setData` re-creates the row
    ([doc-viewport L68-L74]).

### 2.6 What is Community and what is Enterprise

`ag-grid-community` is MIT-licensed ([ag-licence-community]). `ag-grid-enterprise` is under AG
Grid's commercial EULA ([ag-licence-enterprise]); its source was read here for research only, and
nothing of it is copied into this note.

| Mechanism | Licence | Where |
|---|---|---|
| `rowData` with `getRowId`, `applyTransaction`, `applyTransactionAsync`, `flushAsyncTransactions` | Community | [ag-csrmmodule] |
| `deltaSort`, the flat filter and sort stages, the value cache, `refreshCells`, `redrawRows` | Community | [ag-sortstage], [ag-filterstage], [ag-valuecache] |
| Cell flashing and the animated-change renderers | Community | [ag-hcmodule] |
| Grouping, aggregation, the changed path, `aggregateOnlyChangedColumns` | Enterprise | [age-cpfactory]; [doc-grouping] and [doc-aggregation] are marked `enterprise: true` |
| Pivot mode, pivot result columns, `pivotMaxGeneratedColumns` | Enterprise | [doc-pivoting], [doc-pivotcols] are marked `enterprise: true` |
| Cell ranges | Enterprise | [age-range] |
| The Viewport Row Model | Enterprise | [doc-viewport] |

---

## 3. ExGrid, ExGrid.Data and ExPivot today

### 3.1 How a change reaches ExGrid

- **In the push form, a change is a new Window.** The Consumer hands over new parameters, and the
  grid re-reads the Window, its start, its total and the Row Sequence Version in one place
  (`src/ExGrid/Components/ExGrid.razor:2528-2545`).
- **In the pull form, a Grid Source raises `StateChanged`.** The grid marshals it onto the
  renderer's context, re-runs the same `ApplyState` and renders
  (`src/ExGrid/Components/ExGrid.razor:2499-2526`). The interface asks a source to raise it only
  when something painted moved, because the grid re-runs its whole pipeline on it
  (`src/ExGrid/IGridSource.cs:15-17`).
- **A new Window instance is checked for repeated rows.** Every row of it goes through a
  reference-equality set, and a repeat or a null is refused by name
  (`src/ExGrid/Components/ExGrid.razor:2689-2693`, `:2858-2875`). The cost is one pass over the
  whole Window per new Window instance — for `GridSource.From`, the whole result.
- **Selection is reconciled by the Row Sequence Version.** It is dropped when the version moved, or
  when the visible columns changed, and an open editor's text is discarded and announced
  (`src/ExGrid/Components/ExGrid.razor:2746-2768`, `:2785-2800`; ADR-0011).

### 3.2 How a row decides to render, and what a changed row costs

- **One component per row, and the cells are plain markup.** `ExGridRow` writes `ShouldRender` by
  hand, comparing the row by reference and every other parameter by reference or value
  (`src/ExGrid/Components/ExGridRow.razor:302-339`; ADR-0003).
- **Each row is keyed by its instance.** The key is an object held per instance in a
  `ConditionalWeakTable`, so that `@key` is reference identity even for a record type with value
  equality (`src/ExGrid/Components/ExGrid.razor:446`, `:970-974`, `:8681`).
- **An unchanged instance costs nothing.** Scrolling one row keeps the overlapping rows' components
  and renders none of them (`tests/ExGrid.Components/VirtualisationTests.cs:87-100`). A
  fresh-but-equal Window re-renders no surviving row (RR-5,
  [Definition of Done §19](../definition-of-done.md)).
- **A changed instance is a new key, so Blazor discards the old row and builds a new one.**
  - Microsoft's documentation: when a keyed instance changes, `@key` forces Blazor to "discard the
    entire `<li>` or `<div>` and their descendants" and "rebuild the subtree within the UI with new
    elements and components" ([ms-key]).
  - The layer-2 test of a replaced row shows it. After a new `Beta` instance is pushed, every row's
    `RenderCount` is 1, the new `Beta` row's included. A retained component would have rendered a
    second time (`tests/ExGrid.Components/ChangeHighlightTests.cs:227-246`).
- **What a retained component would cost instead.** Blazor's diff of a retained element emits
  `UpdateText` only for a text frame whose content differs ([bl-diff-text]), and `SetAttribute` only
  for an attribute whose value differs ([bl-diff-attr]). A component reached under a matching key
  keeps its instance and is given its new parameters ([bl-diff-component]). A new subtree is sent
  frame by frame — every element, attribute and text ([bl-diff-insert]) — after the removal of the
  old one ([bl-diff-remove]).
- **This is the one place ExGrid departs from ag-grid's repaint.** ag-grid pairs a changed row with
  its previous self by id and refreshes it in place (§2.3). ExGrid has no way to pair them, because
  Row Identity is the reference: ADR-0068 says so in deciding the Change Highlight, "the grid has
  none".

### 3.3 The bundled Grid Sources

- **`GridSource.From` copies the rows once**, so the base stays immutable
  (`src/ExGrid/InMemoryGridSource.cs:22-29`).
- **Its one write, `ReplaceRow`**, finds the row by reference, replaces it, and re-runs the whole
  query — filter and stable sort over every row (`src/ExGrid/InMemoryGridSource.cs:115-170`, `:148`;
  `src/ExGrid/GridQueryEngine.cs:17-34`, `:250-287`).
  - It bumps the Row Sequence Version only when the sequence moved, mapping the replaced instance
    across.
  - There is no form that replaces several rows, adds or removes one.
- **A sort or filter change** re-runs the whole query too, and bumps the version only when the
  sequence moved (`src/ExGrid/InMemoryGridSource.cs:263-300`).
- **`GridSource.Fetch` has no entry for "the server's data moved on".** Its public members are the
  column, sort, filter, range, copy, value-list and Find calls and `Dispose`
  (`src/ExGrid/FetchingGridSource.cs:120`, `:199`, `:211`, `:230`, `:441`, `:474`, `:492`, `:507`).
  A sort or filter change drops the Window and bumps the version (ADR-0025).

### 3.4 `/grid-live`: the Consumer's half of ag-grid's immutable update, written by hand

The demo pushes a Window of a server's trades, and applies the server's notices itself
(`samples/ExGrid.DemoPages/Pages/GridLivePage.razor`). Set beside §2.1, it is the immutable
`rowData` path:

- **Pairing by key.** It keeps a map from `TradeId` to position (`:246-252`), and replaces each
  changed trade in view by a new instance at its position (`:347-355`).
- **Order.** A position that names another trade bumps the Row Sequence Version (`:228-237`), and a
  cancelled trade reads the Window again (`:341-345`).
- **Gathering.** Notices that arrive while a read is out are gathered into the next read, one
  question at a time (`:282-315`, `:182-184`).
- **Which cells changed.** It compares each column's value, old against new, for the trades in view,
  and records the change time that `CellChangedAt` answers (`:356-380`). The delegate is held in a
  field and never replaced, so only the rows with new instances ask again (`:255-261`).

### 3.5 The Change Highlight

- **The rule** (ADR-0068). The Consumer answers when a cell's shown value last changed. The grid
  paints `ex-changed` until that time plus `ChangeHighlightDuration`, keyed by row and column, and
  never compares values itself.
- **One timer.** Each row reports the earliest end among its marks, and the grid keeps one timer for
  the earliest of all. When it fires, only the rows whose marks ended render again
  (`src/ExGrid/Components/ExGridRow.razor:556-607`;
  `src/ExGrid/Components/ExGrid.ChangeHighlight.cs:93-157`).
- **No animation.** The mark is a class added and removed in one step (ADR-0068; ADR-0027 P8).

### 3.6 ExGrid.Data: the Snapshot and the Change Batch

- **A Change Batch makes the next Snapshot, whole or not at all** (ADR-0064;
  `src/ExGrid.Data/Snapshot.cs:192-205`).
- **The result says what changed.** `SnapshotChange` carries `Before`, `After`, the rows `Removed`
  (removed records, and the old versions of changed ones) and the rows `Added` (added records, and
  the new versions of changed ones) — "what a reader needs to fold the batch into what it computed
  rather than start again" (`src/ExGrid.Data/ChangeBatch.cs:76-104`). This is ag-grid's set of
  changed rows (§2.2), stated by the data rather than worked out by a grid.
- **ExGrid does not read Snapshots yet.** ADR-0064 leaves ExGrid's adoption to an ADR of its own,
  "each after measuring", and says a Snapshot serves a grid "as an index beside those objects, never
  as their replacement".

### 3.7 ExPivot

- **The bundled source folds a batch in.** `SnapshotPivotSource.Apply` makes the next Snapshot,
  brings the answer it holds up to date from what the batch removed and added (or defers the fold
  while an answer is being assembled), and raises `Changed` with the new Source Version
  (`src/ExPivot.Engine/SnapshotPivotSource.cs:222-254`; ADR-0067).
- **The component gathers changes on a clock.** A change is asked for at once when the last one
  reached the screen more than `RedrawInterval` ago (250 ms by default), and otherwise when that time
  comes. A data change never cancels a question that is out, and a user's gesture supersedes a
  question for newer data (`src/ExPivot/Components/ExPivot.Live.cs:8-19`, `:124-168`; ADR-0067,
  PV-35).
- **Putting a report on screen** (`src/ExPivot/Components/ExPivot.Asking.cs:636-662`).
  - The Row Sequence Version is bumped only when the report's rows differ from the previous one's.
  - A data version extends the Change Highlight's history; anything else starts a new one.
  - The history pairs a cell with its previous version by what it stands for — the row's role,
    Value Field and Items, and the column's name — and compares painted text, so a change the
    number format hides is not marked (`src/ExPivot/Components/ReportHistory.cs:7-21`).
- **Columns are reused by key.** Each `GridColumn` is cached by name, header, index and width, so a
  report whose columns did not change hands ExGrid the same column instances
  (`src/ExPivot/Components/ExPivot.Report.cs:150-204`) — the same idea as ag-grid's reuse of pivot
  result columns by key ([age-pivotcols-apply]).
- **Rows are not reused.** The row type says so: "the row's identity is the grid's change signal
  (ADR-0003), and a new report is new rows" (`src/ExPivot.Engine/PivotReport.cs:236-240`). The report's rows are the
  Window (`src/ExPivot/Components/ExPivot.Report.cs:415-419`).
  - Every redraw therefore hands ExGrid a new instance for every row, and every painted row is
    disposed and mounted again (§3.2), including rows whose values did not move.
  - ADR-0067 records what this costs end to end today: 43 ms (35–57) from `Apply` to the frame that
    shows 1,000 changes over a million trades, in a published WebAssembly build. That is within the
    0.2 s target (PV-21).

---

## 4. Mapping: each ag-grid mechanism against ExGrid and ExPivot

| ag-grid mechanism | ExGrid / ExPivot counterpart | Status |
|---|---|---|
| Immutable-store rule: a changed row is a new object, an unchanged row the same object ([doc-rowdata L54-L69]) | Row Identity: a new instance is the change signal (ADR-0003) | **Equivalent** |
| `getRowId`: pairs a changed row with its previous self ([ag-nm-immutable]) | None in ExGrid: the instance is the key (`ExGrid.razor:446`). ExPivot pairs report rows by what they stand for, for marks only (`ReportHistory.cs:7-21`). `/grid-live` pairs by `TradeId` by hand | **Gap** — candidates 1, 2 |
| New `rowData` without ids: everything rebuilt, selection reset, scroll to top ([ag-nm-new], [ag-rr-aftermodel]) | A new Window keeps every unchanged instance's row (RR-5); the selection goes only when the Row Sequence Version moves (ADR-0011) | **ExGrid better** |
| The library works out adds, updates and removes from a whole new array by id ([ag-nm-immutable]) | Deferred: "the Consumer hands a whole new Snapshot every time, and the library finds the difference by key" (ADR-0067, Considered options) | **Deferred by ADR** — Open question 5 |
| `applyTransaction` ([ag-nm-tx]) | A Change Batch applied to a Snapshot (ADR-0064); for ExGrid, only `ReplaceRow`, one row at a time (`InMemoryGridSource.cs:115-170`) | **Equivalent in ExGrid.Data and ExPivot; gap in ExGrid** — candidate 3 |
| `applyTransactionAsync`: a 50 ms wait, then one refresh ([ag-csrm-async]) | ExPivot gathers changes, redrawing at most every 250 ms from the newest version (ADR-0067); ExGrid leaves gathering to the Consumer (`GridLivePage.razor:282-315`) | **Equivalent in ExPivot**; for ExGrid, see §5.3 |
| `flushAsyncTransactions` before a gesture ([doc-hf L43-L51]) | A gesture carries the Row Sequence Version it was taken under and is refused if it moved: paste (`src/ExGrid/Clipboard/GridPasteIntent.cs:5-15`), Find (`ExGrid.razor:6581`, `:6601-6607`), Row Marks (`src/ExGrid/Rows/FetchingRowMarks.cs:147-159`) | **ExGrid better** (spine 6) |
| Changed path: re-aggregate only the touched groups, each over all its children ([age-group-delta], [age-agg]) | ExPivot folds a batch into the Leaf Aggregates — exact parts by subtraction and addition, the rest recomputed per touched leaf (ADR-0067) — then computes every cell and total from the leaves, which is report-sized (ADR-0066) | **Different**: work per changed record plus the report's size, against work per touched group's children. Which is cheaper depends on the shape; not measured |
| `deltaSort` ([ag-deltasort]) | None: `GridQueryEngine.Apply` re-sorts every row (`GridQueryEngine.cs:250-287`) | **Gap** — candidate 4 |
| Flat filter re-tests every row, reusing the array when unchanged ([ag-filterstage]) | The same: every row re-tested on each requery (`GridQueryEngine.cs:17-34`); the Row Sequence Version moves only when the sequence moved (`InMemoryGridSource.cs:292-294`) | **Equivalent** |
| Value cache ([ag-valuecache]) | Nothing to cache: a Column's value is a function of its row, read only when the row renders; a Snapshot holds values as columns (ADR-0064) | **Not applicable** |
| Row controllers recycled by id across a model update ([ag-rr-recycle]) | Row components kept by instance (`VirtualisationTests.cs:87-100`); a changed row remounts (`ChangeHighlightTests.cs:227-246`) | **Gap for changed rows** — candidates 1, 2 |
| Per-cell value comparison before a DOM write ([ag-cellctrl-compare]) | Blazor's diff compares text and attributes within a retained row ([bl-diff-text], [bl-diff-attr]) | **Equivalent where the row is retained** |
| `colDef.equals` ([ag-coldef-equals]) | None; the grid never compares values (ADR-0068). ExPivot compares painted text (ADR-0067) | **Rejected for the grid** |
| Cell renderer `refresh()` contract ([doc-renderer L21-L24]) | Cells are plain markup; a Template's fragment is rendered inside the row (`ExGridRow.razor:418-462`; ADR-0003, ADR-0020) | **Not applicable** |
| `refreshCells` / `redrawRows` ([ag-rr-refreshcells], [ag-rr-redrawrows]) | A new delegate (Cell State, `CellChangedAt`) makes every painted row ask again (ADR-0006, ADR-0068); a new instance repaints a row (ADR-0003) | **Equivalent** |
| Cell flash: CSS transition, keyed by cell controller ([ag-flash]) | Change Highlight: no transition, keyed by row and column, asked of the Consumer, one timer (ADR-0068) | **ExGrid deliberately different** |
| Row animation ([doc-rowanim]) | Nothing inside the row Viewport transitions (ADR-0027 P8) | **Rejected** |
| Rows moved by transform; React keeps DOM order ([ag-rowctrl-top], [agr-order]) | Rows flow in DOM order under one translated block (`ExGrid.razor:8896`); Blazor emits a permutation for keyed moves ([bl-diff-permute]) | **Different; not measured** |
| Bounded DOM: rendered range plus buffer, focus and editing rows ([ag-rr-indexes]) | P1: DOM nodes are a function of the Viewport (ADR-0027); Placeholders during a fling (ADR-0004) | **Equivalent or better** (no zombie rows) |
| Animation frames for rows made after a scroll ([ag-rr-createrow], [ag-afs-frame]) | Placeholders during a fling, filled after it settles (ADR-0004); no per-cell JS (P4) | **Equivalent in purpose** |
| Positional cell ranges kept across a sort or new data ([ag-cellrange], [age-range]) | Selection dropped when the Row Sequence Version moves (ADR-0011) | **Rejected** |
| Row selection by node, surviving a reorder | Row Marks by identity, held by the Consumer (ADR-0043) | **Equivalent** |
| Duplicate `getRowId` warns ([ag-errtext]) | A repeated instance in a Window is refused by name (`ExGrid.razor:2858-2875`); two records under one Record Key are refused (ADR-0064) | **ExGrid better** (spine 1) |
| Pivot: re-bucket every row per refresh ([age-pivot-bucket]) | Fold into the Leaf Aggregates (ADR-0067) | **ExPivot better**: no pass over every record per refresh (read from the code; not measured) |
| Pivot result columns reused by key ([age-pivotcols-apply]) | `GridColumn`s cached by key (`ExPivot.Report.cs:150-204`) | **Equivalent** |
| `pivotMaxGeneratedColumns` clears the view ([doc-pivotcols L55-L62]) | A Stale Report: the last report stays, with what happened and as of when (ADR-0067); caps refuse by name (ADR-0066) | **Rejected** (ADR-0067, Q61 option b) |
| `suppressModelUpdateAfterUpdateTransaction` ([ag-csrm-suppress]) | None; the order is the Consumer's, and an order change drops the selection and the editor's text (ADR-0011) | **Open** — Open question 4 |
| Viewport Row Model: the server pushes what is displayed ([doc-viewport L1-L36]) | The push form and Range Requests (ADR-0001); ExPivot's server source answers with Leaf Aggregates (ADR-0066) | **Equivalent in shape** |

---

## 5. How the mechanisms meet the spine

### 5.1 Principle 2 — an immutable base plus a thin difference

- **ag-grid mutates in place.** A row node is a long-lived mutable object whose `data` is replaced
  ([ag-rownode-setdata]). The immutable-store mode asks the *application* to be immutable, and the
  grid then mutates its own nodes to match ([ag-nm-immutable]).
- **The family already has the immutable side.**
  - A Change Batch makes a new Snapshot that shares every untouched part (ADR-0064).
  - An Overlay is a sparse difference over a base (ADR-0007).
  - A changed row is a new instance (ADR-0003).
- **What the family lacks is the pairing.** Nothing tells the grid that a new instance is the next
  version of an old one. That pairing is what ag-grid's id buys it in repainting.
- **The pairing can live on either side of the line.**
  - Outside the grid, it fits principle 3 as it stands: a source knows its Record Keys and can keep
    the instance of every unchanged row (candidates 1 and 3).
  - Inside the grid, it would be a key used only to keep components (candidate 2). It would hold
    nothing between Windows and compare no values, but it is a new concept for the grid.

### 5.2 Principle 3 — the grid neither holds nor executes

- **ag-grid's client-side row model is everything ExGrid does not do**: grouping, filtering,
  pivoting, aggregating, sorting, flattening ([ag-csrm-switch]). ADR-0001 put all of it on the
  Consumer's side.
- **Each mechanism of §2.2 therefore maps to a Consumer-side component, never to ExGrid.**
  - `deltaSort` and incremental filtering map to `GridSource.From` or a new live source
    (candidates 3, 4).
  - The changed path and aggregation map to ExPivot, which already does better (§3.7).
  - Value comparison for marks maps to the source or ExPivot, as ADR-0068 requires.
- **A key used only for `@key` (candidate 2) is the one proposal that touches the grid.** It does
  not make the grid hold or execute anything. It does make the grid accept a second notion of "the
  same row", which `CONTEXT.md` does not yet have; see Open question 1.

### 5.3 Principle 6 — an outcome never depends on timing

- **`applyTransactionAsync`'s final state does not depend on timing.** The queue is applied in
  order, as one refresh ([ag-csrm-async]).
- **What depends on timing is which state a gesture lands on.** A row selected 30 ms after a
  transaction was queued is selected against the old rows. ag-grid's remedy is a synchronisation
  point the application must remember to call: "flush the Async Transaction queue"
  ([doc-hf L43-L51]). Under principle 6 that remedy narrows the race rather than closing it.
- **The family's remedy closes it.** A gesture carries the Row Sequence Version it was taken
  against, and is refused or discarded when it no longer holds: the paste intent
  (`GridPasteIntent.cs:5-15`), Find (`ExGrid.razor:6601-6607`), the Row Marks
  (`FetchingRowMarks.cs:147-159`). Nothing in ag-grid's design needs to be adopted here.
- **ExPivot's gathering (ADR-0067) waits on a clock, and it stays within principle 6.**
  - No report is computed from half a batch.
  - What reaches the screen is the newest version, whatever the timing.
  - The layer-2 tests drive a fake `TimeProvider` (PV-35).
  - The gestures carry the version they were taken under.
- **One timing-dependent effect remains, worth knowing.** ExPivot's Change Highlight compares
  consecutive *shown* versions (`ReportHistory.cs:14-16`). A value that moves and moves back inside
  one redraw interval is never marked, so whether a cell is marked depends on which versions were
  shown. It is
  deterministic under a driven clock, and ag-grid has the same property: a cell flashes only if its
  value differs when the refresh runs ([ag-cellctrl-compare]).
- **Recommendation.** Any gathering for ExGrid's live data belongs in a source (candidate 3), on
  ADR-0067's rules, and never in ExGrid's core. The core has no clock for data: it paints the Window
  it is handed.

### 5.4 The JavaScript allowlist

- **Every ag-grid mechanism here is JavaScript.** None of it moves to the browser in ExGrid.
- **The candidates add no JavaScript.**
  - Keeping a component (candidate 2) is a Blazor diff decision.
  - Gathering is .NET timers, as in ExPivot.
  - Highlighting stays a class (ADR-0068).
- **ag-grid's cell flash relies on two things ExGrid refuses**: per-cell inline style writes, and
  `setTimeout` callbacks that touch elements ([ag-flash]). P4 (no per-cell interop) and P8 (no
  transition inside the Viewport) both stand against them.

### 5.5 Blazor and Blazor Server

- **ag-grid's "refresh in place" is Blazor's retained component.**
  - The diff would then emit edits only for the text and attributes that changed ([bl-diff-text],
    [bl-diff-attr]).
  - A remount sends the whole new row: every element, attribute and text frame ([bl-diff-insert]).
- **On Blazor Server every edit crosses the circuit.**
  - *Prediction (unmeasured):* the bytes per live update grow with the painted cells of every changed
    row under remounting, and with the changed cells alone under retention.
  - *Settled by:* measurement M2 (§8).
- **`ShouldRender` and cached delegates are already right.** A retained row with a new `Row`
  instance renders once; an untouched row does not render (`ExGridRow.razor:302-339`). The
  `/grid-live` page holds its `CellChangedAt` delegate in a field for the same reason
  (`GridLivePage.razor:255-261`; CLAUDE.md, "Cache delegates passed as parameters in a field").
- **Gathering lowers renders per second, and with them round trips.** ExPivot's 250 ms interval
  already caps redraws at four a second (ADR-0067). A Server-hosted ExGrid on a stream gets the same
  only if its Consumer gathers (`GridLivePage.razor:282-315` does).

---

## 6. What ExGrid already does as well as ag-grid, or better

- **Identity-based memoisation.** An unchanged row skips its render entirely, by a hand-written
  `ShouldRender` (ADR-0003, RR-4, RR-5). This is ag-grid's "same reference, assume unchanged" rule,
  applied without a grid-side id.
- **Per-cell write avoidance**, which Blazor's diff provides within a retained row ([bl-diff-text],
  [bl-diff-attr]).
- **Selection that cannot land on the wrong rows.** It is painted by an overlay, never by the rows
  (ADR-0008), and dropped when the order moves (ADR-0011). A gesture carries the version it was
  taken against (§5.3).
- **Refusal instead of a warning** for a repeated row (`ExGrid.razor:2858-2875`) and a repeated
  Record Key (ADR-0064).
- **A Change Highlight that survives scrolling.** It is keyed by row and column and re-asked when a
  row returns, where ag-grid's flash is dropped with its cell controller ([ag-flash]). It is static,
  has one timer, and costs nothing without the declaration (ADR-0068, DC-64 to DC-66).
- **ExPivot's recompute under a pivot.** The batch is folded into the Leaf Aggregates, exact where
  exact is possible (ADR-0067), instead of a re-bucketing pass over every record per refresh
  ([age-pivot-bucket]). This is read from the code, not measured.
- **ExPivot's failure mode.** A Stale Report keeps the last good report and says why and as of when
  (ADR-0067), where ag-grid clears the view at its column cap ([doc-pivotcols L55-L62]).
- **ExPivot's columns**, reused by key (§3.7), as ag-grid's pivot result columns are.
- **A bounded DOM with no transient rows** (P1), where ag-grid keeps fading rows for 400 ms
  ([ag-rr-createupdate]).

## 7. What the family has deliberately rejected

| ag-grid does | Rejected by | Why, in one line |
|---|---|---|
| Animates rows (`animateRows`) and fades flashed cells with a CSS transition | ADR-0027 P8; ADR-0068 Q59a | A transition on a recycled element animates from another row's value; "rewriting P8 to allow a one-off fade" was refused |
| Compares cell values itself to decide what changed (`colDef.equals`, change detection) | ADR-0068 ("The grid never compares values itself (principle 3)") | The grid holds no values between Windows and has no key to tell rows apart |
| Owns a client-side row model: filter, sort, group, aggregate, pivot | ADR-0001; spine 3 | The grid neither sorts nor filters; only the Consumer knows base plus Overlay |
| Keeps positional cell ranges across a reorder or new data | ADR-0011 ("Keep positions as they are" — rejected) | Something invisible must not quietly become something else; a bulk paste follows a selection |
| Re-maps a selection onto "the same rows" | ADR-0011 ("no attempt to re-map") | It degrades the rectangles, costs per selected row, and the grid cannot map identities |
| Clears the view when a pivot cap is passed | ADR-0067 (Q61, option b — rejected); ADR-0066 (caps refuse by name) | An old answer labelled as old, with its time and the reason, is not a plausible wrong answer, and on a live risk screen the last known values are worth keeping in view |
| Works out adds, updates and removes from a whole new array by key | ADR-0067 (Q43, option b — **deferred**, "added if one asks") | It reads every record, and serves only a source that is a periodic full reload |
| Redraws on every change by default (`applyTransaction` is synchronous) | ADR-0067 (Q58, option b — rejected, for ExPivot) | A value changing forty times a second cannot be read, and redraws flicker |

---

## 8. Adoption candidates, ranked

Ranked by expected value against cost. Every "prediction" is unmeasured; the measurements M1 to M5
are listed after the candidates.

### Candidate 1 — ExPivot keeps the instances of report rows whose painted values did not change

- **What.** When a data version is laid out under the same layout and words, ExPivot hands ExGrid
  the previous report's row instance for every row whose role, Items and painted cells are unchanged,
  and a new instance only for a row that changed or appeared. ReportHistory already pairs rows by
  what they stand for and compares their painted text (`ReportHistory.cs:7-21`); this reuses that
  pairing for repainting, as ag-grid's `getRowId` serves both.
- **Package.** `ExPivot` and `ExPivot.Engine`.
  - A `PivotReportRow` belongs to one report (`PivotReport.cs:270-274`), and a label template checks
    that the row belongs to the report on screen (`ExPivot.Report.cs:545`). Reuse needs either rows
    that do not point at one report, or the check rewritten.
- **ADRs.**
  - It fits ADR-0003 as written: an unchanged row keeps its instance, a changed one gets a new one.
  - It changes the code's stated contract, "a new report is new rows" (`PivotReport.cs:239`), which
    no ADR fixes. Whether that warrants a note in ADR-0059 or ADR-0067 is the user's call (Open
    question 2).
  - No ExGrid ADR changes.
- **Prediction.** A live redraw renders only the rows that changed, instead of remounting every
  painted row.
  - In a pivot, one record's change moves its leaf cell, every subtotal above it and the grand total.
    The share of painted rows that change per redraw is therefore what decides the gain, as ADR-0008
    found for selection.
  - With 1,000 changes spread over many Items, most painted rows may change anyway, and the gain
    would be small.
- **Measurement.** M3: count, per live redraw on `/pivot-live`'s generator, the rows that mount and
  the rows that render. This is deterministic, and could become a layer-2 criterion of the RR kind.
  Then re-run ADR-0067's `Apply` → frame observation.

### Candidate 2 — an opt-in key that ExGrid uses only to keep a row's component

- **What.** A Consumer declaration, a function from a row to a value unique in the Window (a Record
  Key, or a report row's key). ExGrid would use it as the row's `@key` in place of the instance key
  (`ExGrid.razor:446`).
  - `ShouldRender` keeps comparing the row by reference, so Row Identity stays the change signal.
  - A changed row would then re-render inside its existing component, and Blazor's diff would write
    only the cells that changed (§3.2). This is ag-grid's `getRowId` for the rendering half only.
- **Package.** `ExGrid` core. ExPivot (with ReportHistory's key) and the live source of candidate 3
  would declare it; ExSheet could.
- **ADRs.**
  - **A new ADR is needed.**
  - It refines ADR-0003's consequence "a different instance" (which component repaints a changed
    row).
  - It qualifies ADR-0068's statement that the grid has no key. It still compares nothing; the key
    only pairs components.
  - It adds a term to `CONTEXT.md`. Row Identity's `_Avoid_` list includes "key, id", and the Record
    Key is ExGrid.Data's, so the name is the user's to choose.
  - Spine 1 asks that a repeated key be refused by name, as a repeated instance is today
    (`ExGrid.razor:2858-2875`), before Blazor's own exception for clashing keys ([ms-key], "Values
    to use for `@key`").
  - Spine 6 asks the ADR to say which instance a press on an Action or Template cell names when the
    row's instance changed between the paint and the press. Today the painting component is disposed
    with its instance; under a key it would carry on with the new one.
  - ADR-0027 P7 and the Interactive cell's engagement (ADR-0037) need re-reading against a component
    that outlives its row instance.
- **Prediction.** For a changed row, the browser's DOM work falls from the row's every element,
  attribute and text to its changed texts and classes. The .NET render cost of the row stays about
  the same, and a component construction and `OnInitialized` are saved. On Blazor Server, the bytes
  per update fall in the same proportion.
- **Measurement.** M1 and M2.

### Candidate 3 — a bundled live Grid Source over a Snapshot and Change Batches

- **What.** The Consumer-side half of ag-grid's immutable update, bundled as `GridSource.From` is.
  It is the ExGrid adoption that ADR-0064 and ADR-0067 announce ("ExGrid and ExSheet take live data
  up in ADRs of their own"). It would:
  - take a Change Batch and use `SnapshotChange.Removed` and `Added`
    (`src/ExGrid.Data/ChangeBatch.cs:76-104`) as ag-grid uses its changed rows;
  - keep the row instance of every record the batch did not touch, and make a new one for each
    changed or added record (ADR-0003);
  - bump the Row Sequence Version only when the sequence moved (`InMemoryGridSource.cs:292-294`'s
    rule; ag-grid's `reordered` flag, [ag-nm-immutable]);
  - answer `CellChangedAt` by comparing the painted text of a changed record's old and new versions,
    as ExPivot does (ADR-0067's rule), replacing the hand-written comparison in `/grid-live`
    (`GridLivePage.razor:356-380`);
  - optionally gather batches on an interval, under ADR-0067's rules: never half a batch, always the
    newest version, a fake `TimeProvider` in tests;
  - declare candidate 2's key, if candidate 2 is taken.
- **Package.** `ExGrid` taking a reference to `ExGrid.Data` — which has no dependency (ADR-0064), so
  the core would still depend on no design system or state library — or a new package beside it.
  The user decides (Open question 3).
- **ADRs.** A new ADR, already announced by ADR-0064 and ADR-0067. ADR-0064 requires measuring
  first, and says a Snapshot is "an index beside those objects, never … their replacement": the rows
  ExGrid paints stay the Consumer's objects, or row views over the Snapshot. That choice is part of
  the ADR.
- **Prediction.** Removes the Consumer code that `/grid-live` carries, and gives ExGrid's live
  screens the same three guarantees as ExPivot's: never half a batch, selection kept on a
  values-only change, and marks from the data alone.
- **Measurement.** M4, M5, and PV-21's "1,000 changes ≤ 0.2 s" target stated for ExGrid.

### Candidate 4 — incremental requery in the bundled sources (ag-grid's `deltaSort`)

- **What.** Rather than re-running filter and sort over every row (`InMemoryGridSource.cs:148`,
  `:282`):
  - re-test only the changed and added rows against the Filter;
  - drop the removed and changed rows from the previous result;
  - sort the changed rows alone, with the original position breaking ties;
  - merge them into the previous order ([ag-deltasort]).
  It needs a way to hand over many rows at once (a `ReplaceRows`, or candidate 3's Change Batch).
- **Package.** `ExGrid` (`InMemoryGridSource`, `GridQueryEngine`), or candidate 3's source.
- **ADRs.**
  - No change of meaning: ADR-0023's semantics are the specification, and the result must equal
    `GridQueryEngine.Apply` exactly, including the stable tie order (`GridQueryEngine.cs:281`).
  - A batch method is new public API on the reference implementation. Whether that needs an ADR or
    rides on candidate 3's is the user's call.
- **Prediction.** It wins when the changed rows are few against the result, and loses for batches
  large against the data — ag-grid's own warning ([doc-tx L146-L166]) — and is not worth its
  overhead on a handful of rows, where ag-grid falls back to a full sort ([ag-deltasort]).
- **Measurement.** M4, for the speed. **M5 gates its correctness**: a property test, the incremental
  result against a full `GridQueryEngine.Apply` over random batches, as PV-34 does for ExPivot's
  leaves.

### Candidate 5 — grid-side work per update that grows with the Window

- **What.**
  - Every new Window instance is passed through `RequireDistinctRows`, a reference set over the whole
    Window (`ExGrid.razor:2689-2693`).
  - Under `GridSource.From` the Window is the whole result, so each `ReplaceRow` costs the grid one
    pass over every row before any row renders.
  - ag-grid checks only the nodes it creates ([ag-nm-create]).
- **Package.** `ExGrid` core.
- **ADRs.** The check is a refusal that spine 1 requires, and it must not be weakened (CLAUDE.md,
  "Do not weaken a requirement"). Any change keeps the refusal and finds a cheaper route to it, for
  example a check over only the positions that moved, when a bundled source states them. That would
  be a decision; this candidate is listed so that the cost is seen, not settled.
- **Prediction.** Linear in the Window: small at today's demo sizes, and visible at 10⁶ rows with
  frequent `ReplaceRow` calls.
- **Measurement.** M4 includes the grid's `ApplyState` at 10⁵ and 10⁶ rows.

### Not proposed

- **A timed wait in ExGrid's core** (`asyncTransactionWaitMillis`). The core has no clock for data,
  and gathering belongs in sources (§5.3).
- **A flush API.** Gestures already carry their version (§5.3).
- **A value cache**, a renderer `refresh()` contract, an `equals` hook, row animation, or keeping
  ranges across a reorder (§4, §7).

### Measurements

All are observational and never gate (CLAUDE.md, "Never gate on performance"), except M5, which is a
correctness test.

| | What | Where | Settles |
|---|---|---|---|
| **M1** | A live tick: k of 40 painted rows replaced by new instances per frame (k = 1, 5, 40), at 20 and 50 columns. Keyed by instance (today) against keyed by a stable key with reference `ShouldRender`. Median, p95 and max render ms, as ADR-0003 recorded, plus the count of DOM edits per tick | A new mode in `spikes/render-bench` | Candidate 2's prediction, and how much candidate 1 saves per unchanged row |
| **M2** | The bytes per live update on the Server host, for the same ticks | Layer 3 on the Server host, reading WebSocket frame sizes; observational, as BIG-6 is | The circuit half of candidate 2 |
| **M3** | Rows mounted and rows rendered per live redraw on `/pivot-live`'s generator; then `Apply` → frame | Layer 2 render counts; the existing measure spec | Candidate 1 |
| **M4** | `ReplaceRow` (or a batch) at 10⁵ and 10⁶ rows with 1, 100 and 1,000 changes: the source's requery and the grid's `ApplyState`, full against incremental | A spike under `spikes/`, since the repository has no benchmark harness outside `render-bench` | Candidates 3, 4, 5 |
| **M5** | The incremental result equals `GridQueryEngine.Apply` over random batches, sorts and filters | Layer 1 property test (correctness — this one gates) | Candidate 4's correctness |

---

## 9. Open questions for the user

These are proposals. Each needs a decision before implementation, and the ADR that records it is
the orchestrator's to write (CLAUDE.md, "Working in parallel").

1. **Should ExGrid accept an opt-in key used only to keep a row's component (candidate 2)?** It
   leaves Row Identity as the change signal, but gives the grid a second notion of "the same row".
   It needs a new ADR, a new term in `CONTEXT.md`, and a note on ADR-0003 and ADR-0068. The
   alternative is to keep the grid keyless, leave reuse to the sources (candidates 1 and 3), and
   accept that a changed row always remounts. M1 and M2 are proposed first, so the decision is made
   on numbers.
2. **Should ExPivot reuse the instances of unchanged report rows (candidate 1)?** It changes
   `PivotReportRow`'s stated contract, "a new report is new rows". Is a note in ADR-0059 or ADR-0067
   wanted, or is it ExPivot's implementation alone? M3 is proposed first.
3. **ExGrid's live-data ADR (candidate 3), which ADR-0064 and ADR-0067 announce: what should it
   cover?**
   - Does ExGrid's core take a reference to `ExGrid.Data`, or does the live source ship as a package
     of its own?
   - Does the source gather on an interval as ExPivot does (250 ms by default), or apply each batch?
   - Are the rows it hands the grid the Consumer's objects, or row views over the Snapshot?
4. **When a sorted column's values change under a live feed, should the order follow at once?** Today
   it does, and ADR-0011 drops the selection, and an open editor's text, each time the sequence
   moves. On a screen sorted by P&L that may be four times a second. ag-grid offers to hold the
   order on update-only changes so that rows do not move under an edit
   ([ag-csrm-suppress], [doc-tx L127-L144]). If the family wants that, spine 1 asks that the
   held order be shown — a sort indicator that claims an order the rows no longer have is quietly
   wrong — and the choice is a source's, under an ADR.
5. **Should the family find the difference between two whole arrays by key, as ag-grid's immutable
   `rowData` does?** ADR-0067 deferred it until a Consumer whose only source is a periodic full
   reload asks. ExGrid Consumers that re-query a server and push the whole Window are that Consumer.
   This is a question of whether the trigger has come, not a recommendation.
6. **Candidate 4's batch method on `GridSource.From`**: does it need an ADR of its own, or does it
   ride on question 3's?

---

## 10. Sources

### ag-grid documentation

Published pages on ag-grid.com, read through their source files under
`documentation/ag-grid-docs/src/content/docs/` at the pinned commit; the line-numbered references
in the text point at those source files.

- [Updating Data][doc-update]
- [Updating Row Data][doc-rowdata]
- [Client-Side Data — Transaction Updates][doc-tx]
- [Client-Side Data — High Frequency Updates][doc-hf]
- [Client-Side Data — Single Row / Cell Updates][doc-single]
- [Change Detection][doc-cd]
- [View Refresh][doc-viewrefresh]
- [Value Cache][doc-vc]
- [Highlighting Changes][doc-flash]
- [Row Animation](https://www.ag-grid.com/javascript-data-grid/row-animation/) (source: [doc-rowanim])
- [Cell Components][doc-renderer]
- [Pivot Result Columns](https://www.ag-grid.com/javascript-data-grid/pivoting-result-columns/)
  (source: [doc-pivotcols])
- [Viewport Row Model](https://www.ag-grid.com/javascript-data-grid/viewport/) (source:
  [doc-viewport])
- The `enterprise: true` flags of [Pivoting][doc-pivoting], [Row Grouping][doc-grouping] and
  [Aggregation][doc-aggregation]

### ag-grid source

Commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`; the reference definitions are at the end of
this file. Labels beginning `ag-` are under `packages/ag-grid-community/src`, `age-` under
`packages/ag-grid-enterprise/src`, and `agr-` under `packages/ag-grid-react/src`. The licences:
[MIT][ag-licence-community] for Community, [the AG Grid EULA][ag-licence-enterprise] for Enterprise.

### Blazor

- Microsoft Learn, [Retain element, component, and model relationships in ASP.NET Core
  Blazor][ms-key] (`@key`), ASP.NET Core 10.0: the sections "When to use `@key`", "When not to use
  `@key`" and "Values to use for `@key`".
- `RenderTreeDiffBuilder.cs` in `dotnet/aspnetcore` at tag `v10.0.12` (commit
  `cb21a42eafcd44cc35fad48d99dc82ff7512ce2f`): a text frame ([bl-diff-text]), an attribute
  ([bl-diff-attr]), a retained component ([bl-diff-component]), a new subtree ([bl-diff-insert]), a
  removed one ([bl-diff-remove]), and the permutation list for keyed moves ([bl-diff-permute]).

### This repository (at `60d522ed`)

- ADRs: [0001](../adr/0001-consumer-pushes-the-window-grid-does-not-fetch.md),
  [0003](../adr/0003-cells-are-plain-markup-by-default-not-components.md),
  [0004](../adr/0004-cap-the-cells-touched-per-frame.md),
  [0006](../adr/0006-grid-owns-a-generic-cell-state-vocabulary.md),
  [0007](../adr/0007-edits-are-an-overlay-owned-by-the-consumer.md),
  [0008](../adr/0008-selection-is-painted-by-an-overlay.md),
  [0011](../adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md),
  [0020](../adr/0020-action-and-template-columns.md),
  [0021](../adr/0021-javascript-is-allowlisted-not-minimised.md),
  [0023](../adr/0023-filter-and-sort-semantics-of-the-reference-implementation.md),
  [0025](../adr/0025-what-the-bundled-fetching-source-promises.md),
  [0027](../adr/0027-appearance-travels-in-css-geometry-travels-in-csharp.md),
  [0037](../adr/0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md),
  [0043](../adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md),
  [0059](../adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
  [0060](../adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md),
  [0064](../adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md),
  [0066](../adr/0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md),
  [0067](../adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md),
  [0068](../adr/0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md).
- [Definition of Done](../definition-of-done.md): §16 (PF), §19 (RR), §26 (DC-64 to DC-66),
  §29 (PV-21, PV-34 to PV-38).
- [`CONTEXT.md`](../../CONTEXT.md): Row Identity, Window, Row Sequence Version, Snapshot, Change
  Batch, Record Key, Change Highlight, Overlay.
- Code: as cited inline, `path:line`.

<!-- ag-grid documentation sources at the pinned commit -->
[doc-update L46-L62]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update/index.mdoc#L46-L62
[doc-update]: https://www.ag-grid.com/javascript-data-grid/data-update/
[doc-rowdata]: https://www.ag-grid.com/javascript-data-grid/data-update-row-data/
[doc-rowdata L16-L24]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-row-data/index.mdoc#L16-L24
[doc-rowdata L54-L69]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-row-data/index.mdoc#L54-L69
[doc-rowdata L54-L97]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-row-data/index.mdoc#L54-L97
[doc-rowdata L81-L97]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-row-data/index.mdoc#L81-L97
[doc-tx]: https://www.ag-grid.com/javascript-data-grid/data-update-transactions/
[doc-tx L33-L83]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-transactions/index.mdoc#L33-L83
[doc-tx L100-L118]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-transactions/index.mdoc#L100-L118
[doc-tx L120-L125]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-transactions/index.mdoc#L120-L125
[doc-tx L127-L144]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-transactions/index.mdoc#L127-L144
[doc-tx L146-L166]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-transactions/index.mdoc#L146-L166
[doc-hf]: https://www.ag-grid.com/javascript-data-grid/data-update-high-frequency/
[doc-hf L43-L51]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-high-frequency/index.mdoc#L43-L51
[doc-single]: https://www.ag-grid.com/javascript-data-grid/data-update-single-row-cell/
[doc-single L22-L34]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/data-update-single-row-cell/index.mdoc#L22-L34
[doc-cd]: https://www.ag-grid.com/javascript-data-grid/change-detection/
[doc-cd L31-L68]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-detection/index.mdoc#L31-L68
[doc-cd L70-L89]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-detection/index.mdoc#L70-L89
[doc-cd L149-L164]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-detection/index.mdoc#L149-L164
[doc-cd L180-L202]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-detection/index.mdoc#L180-L202
[doc-cd L216-L238]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-detection/index.mdoc#L216-L238
[doc-viewrefresh]: https://www.ag-grid.com/javascript-data-grid/view-refresh/
[doc-viewrefresh L9-L15]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/view-refresh/index.mdoc#L9-L15
[doc-viewrefresh L58-L66]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/view-refresh/index.mdoc#L58-L66
[doc-vc]: https://www.ag-grid.com/javascript-data-grid/value-cache/
[doc-vc L47-L56]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/value-cache/index.mdoc#L47-L56
[doc-vc L84-L91]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/value-cache/index.mdoc#L84-L91
[doc-flash]: https://www.ag-grid.com/javascript-data-grid/change-cell-renderers/
[doc-flash L15-L56]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/change-cell-renderers/index.mdoc#L15-L56
[doc-rowanim]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/row-animation/index.mdoc#L5-L39
[doc-renderer]: https://www.ag-grid.com/javascript-data-grid/component-cell-renderer/
[doc-renderer L21-L24]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/component-cell-renderer/_component-interface-javascript.mdoc#L21-L24
[doc-pivotcols]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/pivoting-result-columns/index.mdoc#L1-L5
[doc-pivotcols L55-L62]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/pivoting-result-columns/index.mdoc#L55-L62
[doc-viewport]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/viewport/index.mdoc#L1-L4
[doc-viewport L1-L36]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/viewport/index.mdoc#L1-L36
[doc-viewport L68-L74]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/viewport/index.mdoc#L68-L74
[doc-pivoting]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/pivoting/index.mdoc#L1-L5
[doc-grouping]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/grouping/index.mdoc#L1-L5
[doc-aggregation]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/documentation/ag-grid-docs/src/content/docs/aggregation/index.mdoc#L1-L5

<!-- ag-grid community source -->
[ag-licence-community]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/LICENSE.txt
[ag-licence-enterprise]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/LICENSE.html
[ag-csrm-stages]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L186-L207
[ag-csrm-onprop]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L333-L381
[ag-csrm-suppress]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L638-L654
[ag-csrm-refresh]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L673-L742
[ag-csrm-execute]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L745-L798
[ag-csrm-flat]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L767-L770
[ag-csrm-switch]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L772-L793
[ag-csrm-async]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L1213-L1268
[ag-csrm-update]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts#L1274-L1307
[ag-nm-new]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L25-L84
[ag-nm-immutable]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L86-L156
[ag-nm-delete]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L158-L177
[ag-nm-tx]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L179-L299
[ag-nm-create]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L305-L321
[ag-nm-lookup]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L336-L347
[ag-nm-lookupref]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L409-L428
[ag-changedrownodes]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/changedRowNodes.ts#L1-L9
[ag-changedpath-walk]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/utils/changedPath.ts#L102-L136
[ag-sortstage]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/sortStage.ts#L48-L90
[ag-deltasort]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/deltaSort.ts#L7-L93
[ag-filterstage]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/filterStage.ts#L21-L97
[ag-valuecache]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/valueService/valueCache.ts#L6-L41
[ag-rownode-setdata]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/rowNode.ts#L346-L407
[ag-rownode-setdatavalue]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/rowNode.ts#L571-L631
[ag-changedet]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/valueService/changeDetectionService.ts#L58-L146
[ag-pagebounds]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/pagination/pageBoundsListener.ts#L18-L29
[ag-rr-timeout]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L47
[ag-rr-pageloaded]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L477-L490
[ag-rr-redrawrows]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L586-L650
[ag-rr-aftermodel]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L658-L724
[ag-rr-refreshcells]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L887-L905
[ag-rr-recycle]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1040-L1061
[ag-rr-indexes]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1153-L1250
[ag-rr-createupdate]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1311-L1397
[ag-rr-unvirt]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1564-L1585
[ag-rr-createrow]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1594-L1612
[ag-rowctrl-instance]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/rowCtrl.ts#L170
[ag-rowctrl-listen]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/rowCtrl.ts#L612-L636
[ag-rowctrl-datachanged]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/rowCtrl.ts#L702-L718
[ag-rowctrl-top]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/rowCtrl.ts#L1395-L1407
[ag-normalrow-refresh]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/normalRowFeature.ts#L40-L46
[ag-normalrow-listen]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/row/normalRowFeature.ts#L346-L360
[ag-cellctrl-show]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellCtrl.ts#L413-L453
[ag-cellctrl-refresh]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellCtrl.ts#L590-L699
[ag-cellctrl-compare]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellCtrl.ts#L732-L760
[ag-cellcomp]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellComp.ts#L158-L199
[ag-cellcomp-text]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellComp.ts#L305-L313
[ag-cellcomp-refresh]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellComp.ts#L354-L373
[ag-flash]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/cellFlashService.ts#L25-L137
[ag-hcmodule]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/cell/highlightChangesModule.ts#L9-L24
[ag-csrmmodule]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideRowModelModule.ts#L23-L55
[ag-afs]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/misc/animationFrameService.ts#L56-L59
[ag-afs-frame]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/misc/animationFrameService.ts#L122-L217
[ag-defaults]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/gridOptionsDefault.ts#L71-L189
[ag-opt-pivotmax]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/gridOptions.ts#L1219-L1225
[ag-opt-async]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/gridOptions.ts#L1778-L1786
[ag-opt-scroll]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/gridOptions.ts#L1929-L1945
[ag-opt-delta]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/gridOptions.ts#L2168-L2174
[ag-opt-getrowid]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/gridOptions.ts#L2516-L2526
[ag-coldef-equals]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/colDef.ts#L377-L381
[ag-coldef-flash]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/colDef.ts#L836-L841
[ag-errtext]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/validation/errorMessages/errorText.ts#L243-L244
[ag-cellrange]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/interfaces/IRangeService.ts#L71-L80
[ag-rowpos]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/interfaces/iRowPosition.ts#L3-L9

<!-- ag-grid enterprise source (read for research only; nothing copied) -->
[age-cpfactory]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowHierarchy/changedPathImpl/changedPathFactory.ts#L7-L34
[age-groupstage]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowHierarchy/groupStage.ts#L106-L139
[age-group-init]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L162-L187
[age-group-delta]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L107-L238
[age-agg]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/aggregation/aggregationStage.ts#L128-L249
[age-agg-cols]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/aggregation/aggregationStage.ts#L253-L342
[age-agg-pivot]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/aggregation/aggregationStage.ts#L345-L427
[age-aggdata]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/aggregation/aggDataUtils.ts#L35-L135
[age-pivot-exec]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/pivot/pivotStage.ts#L60-L153
[age-pivot-unique]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/pivot/pivotStage.ts#L155-L164
[age-pivot-bucket]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/pivot/pivotStage.ts#L167-L196
[age-pivotcols]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/pivot/pivotResultColsService.ts#L143-L171
[age-pivotcols-apply]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/pivot/pivotResultColsService.ts#L208-L247
[age-range]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rangeSelection/rangeService.ts#L120-L134

<!-- ag-grid React layer -->
[agr-container]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-react/src/reactUi/rows/rowContainerComp.tsx#L74-L164
[agr-order]: https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-react/src/reactUi/utils.tsx#L120-L190

<!-- Blazor -->
[ms-key]: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/element-component-model-relationships?view=aspnetcore-10.0
[bl-diff-permute]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L290-L313
[bl-diff-text]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L580-L591
[bl-diff-component]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L695-L716
[bl-diff-attr]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L795-L811
[bl-diff-insert]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L830-L889
[bl-diff-remove]: https://github.com/dotnet/aspnetcore/blob/cb21a42eafcd44cc35fad48d99dc82ff7512ce2f/src/Components/Components/src/RenderTree/RenderTreeDiffBuilder.cs#L891-L939
