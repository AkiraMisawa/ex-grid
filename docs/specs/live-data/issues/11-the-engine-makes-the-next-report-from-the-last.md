# 11: The engine makes the next cube and report from the last

Status: ready-for-agent

**What to do:** build the core of [ADR-0161](../../../adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md). Under the same layout, a data redraw starts from the
cube and report on screen and the leaves that changed. It keeps the axis nodes, computes again the cells
on the changed paths, and shares every row whose path did not change. Nothing is rewritten in place.

**Blocked by:** 09, 10

## What to build

- **Which leaves changed:**
  - `PivotAnswer` gains the optional changed leaves since a Source Version the question names
    (ADR-0066's note), and `SnapshotPivotSource` supplies them from its fold.
  - Without them, the engine compares the leaves of the new answer with the previous one. Measure what
    that comparison costs at 400,000 leaves.
- **The next cube** shares the axis trees and copies the values it changes. **The next report** shares
  the rows off the changed paths.
- **The redraws that start afresh**, exactly ADR-0161's list: data-free changes, a change of structure,
  a batch the source could not fold, an order that follows values, a Show Values As that reads other
  rows, and the engine's own threshold of changed leaves.
- **`ExPivot.Asking.cs`** takes the path. PV-40's slicing still holds, and the report on screen stays
  until the next is complete.
- **Change Highlight:** compare only the rows on changed paths when the redraw was incremental (10's
  rule, narrowed).

## Done when

- [ ] PV-44's property test passes over random batches and layouts, both with the source's changed
  leaves and without; the seed recorded
- [ ] PV-45 passes: the rows that render per redraw are the painted rows whose text changed
- [ ] LV-22's ExPivot half passes: no report but the one on screen alive after a run of redraws
- [ ] Layer 1 and 2 green; the pivot layer-3 specs pass locally on one host

## Comments
