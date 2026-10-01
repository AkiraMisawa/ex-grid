# 57: The line between two cells, as Excel keeps it

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"What the twelfth Windows run settled", and its model of how Excel keeps the line between two cells.
Ticket 55 wrote both cells' sides whenever an edge was set. The twelfth run showed Excel works
differently, and that model explains every edge answer of the eleventh and twelfth runs. The record
is `verification/2026-10-01-windows-excel-12/cell-format-12.md`.

**Blocked by:** None (can start immediately)

- [ ] **Each cell records its own four sides.**
  - A border command (a key, Format Cells, `SetCellFormatAsync`) records the edge on the cells it
    sets, and clears the neighbour's record of the same edge. It does not write the neighbour's.
  - This replaces ticket 55's mirroring. Undo restores both cells.
- [ ] **Reading or showing an edge** gives the upper cell's line, or the left cell's for a vertical
      edge, where both record one; otherwise whichever records one.
  - `Sheet.GetBorders` and `CellFormatAt` answer the edge as shown, from either side.
  - ExSheet answers the core's question of which line to draw by the same rule (ADR-0050, item 15).
- [ ] **A paste, Ctrl+D, Ctrl+R and the fill handle** write the target cells' own four sides and touch
      no neighbour (cases 1–5). Case 3's edge between the source and its first target shows the
      source's bottom.
- [ ] **Insertion and deletion move cells with their own records**, and an inserted row or column
      takes no Borders.
  - A line on row 1's top moves down with it (case 10).
  - Two rows brought together show the upper row's line, or the lower's when the upper has none
    (cases 6, 7 and 9).
  - Ticket 55's `JoinInserted` / `TakeLineAcross` are no longer needed. Remove them if nothing
    else uses them.
- [ ] **Outlines** (cases 14 and 15).
  - Over a whole row: top and bottom, and the left of column A.
  - Over the whole Sheet: nothing.
  - Over whole columns: left and right, as now. The inside line over whole columns stays on row 1's
    top and row 1048576's bottom (case 16).
- [ ] **The Sheet Document** records each cell's own sides, which is what it records now.
  - A document written by ticket 55's code reads the same edges under the rule above, because its
    two records of an edge agree.
  - No new version.
- [ ] **Tests.**
  - Layer 1, each test named with ADR-0063 and the run's case (11-7, 11-13, 12-1 … 12-16).
  - Ticket 55's tests that pinned the mirroring change to say what Excel does.
