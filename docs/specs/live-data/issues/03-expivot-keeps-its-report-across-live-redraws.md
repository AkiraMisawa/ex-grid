# 03: ExPivot keeps its report across live redraws

Status: done — decided 2026-10-07 as ADR-0161 and built by tickets 09 to 12 on the Claude Code track;
replaced on 2026-10-08, when the user took the Codex track's ExPivot (ADR-0151 to ADR-0153), whose
incremental engine meets this ticket's aim. Of tickets 09 to 12 only 12's rule stayed (ADR-0161)

**The aim:** a live redraw costs what changed, not what the report holds. Today ExPivot builds its
cube, its report and every report row again on every redraw, at most four times a second. At 401,001
report rows building the report alone took 337 ms (D10, ADR-0140). ag-grid keeps its row nodes,
their ids and their rendered rows across updates; this ticket takes that approach for the pivot,
where the ADRs allow it.

**Blocked by:** 01 (which step of a redraw dominates, at which size). Ticket 02 is independent, but
both meet in the grid's check of the report.

## What ExPivot does on every redraw

Each step is in `src/ExPivot/Components/ExPivot.Asking.cs`.

- **The bundled source folds the Change Batch into its Leaf Aggregates** and answers
  (`SnapshotPivotSource.Apply`, ADR-0067). This step is already incremental, and ahead of ag-grid:
  ag-grid's pivot mode buckets every row again on every refresh (research note, §2.4).
- **The cube is made again from the whole answer** (`PivotEngine.CubeAsync`, `:392`). Its axis trees
  are built leaf by leaf (`PivotCube.TreeAsync`, `src/ExPivot.Engine/PivotCube.cs:208`), and each
  node's Item hash and path hash are made with it (D10).
- **The report is laid out again** (`PivotEngine.ReportAsync`, `:544`; `ReportBuilder`). Every row is
  a new `PivotReportRow`, with a new `PivotRowKey`.
- **The new rows are compared with the old ones** (`HasSameRowsAsAsync`, `:554`), and the label
  widths are measured again (`LabelWidthsAsync`, `:557`).
- **`Show` (`:636`)** extends the Change Highlight's history, and hands the grid a new delegate for
  each data version: `_cellChangedAt`, `ExPivot.Report.cs:464`.
- **The grid checks every row's key** (`RequireDistinctKeys`; ticket 02), and renders.
  - Rows are now kept by key (ADR-0140, PV-42), so a changed row repaints in place.
  - Every painted row still renders. The rows are new instances, and the delegate is new; a row
    compares the delegate by reference (`src/ExGrid/Components/ExGridRow.razor:320`).

## What ag-grid keeps, read in its source

All links are at ag-grid 36.2.0, commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`. The research
note has the rest: §2.2 for the changed path, §2.4 for pivot mode.

- **A row's id is computed once**, when its data is set, and kept on the node
  ([`rowNode.ts#L468-L493`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/rowNode.ts#L468-L493)).
- **A group's id is its parent's id, its column and its key**, made when the group is made
  ([`groupStrategy.ts#L523`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L523)).
  An update finds the group already there and keeps it
  ([`#L492-L494`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L492-L494)).
- **The map of ids is checked only when a node is made**
  ([`clientSideNodeManager.ts#L304-L320`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L304-L320)).
- **Aggregation follows the changed path**, except in pivot mode: a new pivot key drops the path, and
  every group is aggregated again (§2.4).

**So ag-grid's advantage under a pivot is not its aggregation**, which the family's folding already
beats. It is that a row node, its id and its rendered row outlive an update.

## What the design must respect

- **ADR-0003.** A changed row is a new instance, so that it repaints. An unchanged one may keep its
  instance, and then skips its render.
- **`PivotReportRow` is a new row per report, by contract.**
  - The contract is written at `src/ExPivot.Engine/PivotReport.cs:239`.
  - A row points at its report (`Report`, set by `Attach`, `:276`/`:280`).
  - A row caches its values on first read (`_cells`, `:244`), from its report's cube.
  - ExPivot ties a context command to the report on screen
    (`ReferenceEquals(context.Row.Report, report)`, `src/ExPivot/Components/ExPivot.Report.cs:564`).
  - A row shared by two reports would answer with the older report's values and point at the wrong
    report, unless the design changes all of this.
- **ADR-0060.** The engine is the reference, and a report is immutable. A next report may share what
  did not change (CLAUDE.md's principle 2: an immutable base plus a thin difference); it is never
  rewritten in place.
- **ADR-0066 and ADR-0067.** Leaf Aggregates; never half a batch; the Stale Report. A layout change,
  new caps, or a batch the source refuses to fold (a compaction, `MaxLeaves`) start afresh.
- **ADR-0068.** The Change Highlight's delegate is the change signal, and today a data version makes a
  new one. Unless one delegate is held across data versions, a kept row still renders (M3).
- **ADR-0140.** The Row Key, `PivotRowKey`, and PV-42 and PV-43.
- **PV-21's targets, and PV-40.** The work after an answer is sliced, and yields every 30 ms.

## Decisions to put to the user

1. **Where it lives.**
   - (a) The engine makes the next cube and report from the previous ones and the leaves that
     changed: axis nodes kept, and rows under unchanged paths shared.
   - (b) ExPivot keeps the instances of rows whose painted values did not change, after a fresh
     build. This is the research note's candidate 1, reserved in §21.11.
   - (c) Both.
2. **What a row is.** Whether `PivotReportRow` stops pointing at one report and caching that report's
   values, or a kept row stays a different object from the engine's. Whether
   `PivotReport.cs:239`'s contract is rewritten.
3. **One Change Highlight delegate across data versions**, so that a kept row skips its render
   (ADR-0068's "a new delegate is the change signal").
4. **When to start afresh.** A layout change, an Item appearing or leaving (the Row Sequence Version
   moves, ADR-0011), a compaction, a cap.
5. **The correctness gate.** A property test, as PV-34 and LV-5 have: over random batches, the kept
   report equals a fresh one, row by row and cell by cell, and a row whose painted text changed is a
   new instance.
6. **Ticket 02's vouch**, if it is not already decided: a kept report still meets the grid's check
   of every key on every new Window.

## Done when (to be confirmed by the ADR)

- [ ] The decisions above, recorded in an ADR (0143 or later; see the spec), with §29 criteria
- [ ] Ticket 01's ExPivot measurement repeated: the redraw at each size, before and after
- [ ] M3 repeated: the rows mounted and rendered per redraw on `/pivot-live`'s generator
- [ ] The property test
- [ ] PV-21's observation in the browser (`measure-pivot.spec.mjs`), recorded beside the old one

## Comments

2026-10-06: Opened by the user's request, after D10 showed that the Row Key's remaining cost and
most of a large redraw's time come from building every row again, which ag-grid avoids by keeping its
nodes.

2026-10-07: Decided with the user (Q9 to Q14, and the marks of a redraw laid out afresh), recorded as
[ADR-0161](../../../adr/0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md).
1. **Where:** (a), the engine makes the next cube and report from the last.
2. **What a row is:** a row holds no value and no report; a value cell is asked of a report; the contract
   at `PivotReport.cs:239` is rewritten.
3. **One delegate:** yes, and the history keeps change times, not reports.
4. **When to start afresh:** ADR-0161's list.
5. **The gate:** a property test, PV-44.
6. **Ticket 02's vouch:** ExPivot vouches.

Changed leaves come from the answer when the source knows them, otherwise from the engine's own
comparison. A redraw laid out afresh under data compares every row for its marks. A redraw that runs
out of memory leaves the report stale. Built by tickets 09 to 12, and measured after by ticket 13.
