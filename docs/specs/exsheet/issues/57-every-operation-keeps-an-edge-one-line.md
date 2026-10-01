# 57: The line between two cells, as Excel keeps it

Status: done

**What to build:** [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"What the twelfth Windows run settled", and its model of how Excel keeps the line between two cells.
Ticket 55 wrote both cells' sides whenever an edge was set. The twelfth run showed Excel works
differently, and that model explains every edge answer of the eleventh and twelfth runs. The record
is `verification/2026-10-01-windows-excel-12/cell-format-12.md`.

**Blocked by:** None (can start immediately)

- [x] **Each cell records its own four sides.**
  - A border command (a key, Format Cells, `SetCellFormatAsync`) records the edge on the cells it
    sets, and clears the neighbour's record of the same edge. It does not write the neighbour's.
  - This replaces ticket 55's mirroring. Undo restores both cells.
- [x] **Reading or showing an edge** gives the upper cell's line, or the left cell's for a vertical
      edge, where both record one; otherwise whichever records one.
  - `Sheet.GetBorders` and `CellFormatAt` answer the edge as shown, from either side.
  - ExSheet answers the core's question of which line to draw by the same rule (ADR-0050, item 15).
- [x] **A paste, Ctrl+D, Ctrl+R and the fill handle** write the target cells' own four sides and touch
      no neighbour (cases 1–5). Case 3's edge between the source and its first target shows the
      source's bottom.
- [x] **Insertion and deletion move cells with their own records**, and an inserted row or column
      takes no Borders.
  - A line on row 1's top moves down with it (case 10).
  - Two rows brought together show the upper row's line, or the lower's when the upper has none
    (cases 6, 7 and 9).
  - Ticket 55's `JoinInserted` / `TakeLineAcross` are no longer needed. Remove them if nothing
    else uses them.
- [x] **Outlines** (cases 14 and 15).
  - Over a whole row: top and bottom, and the left of column A.
  - Over the whole Sheet: nothing.
  - Over whole columns: left and right, as now. The inside line over whole columns stays on row 1's
    top and row 1048576's bottom (case 16).
- [x] **The Sheet Document** records each cell's own sides, which is what it records now.
  - A document written by ticket 55's code reads the same edges under the rule above, because its
    two records of an edge agree.
  - No new version.
- [x] **Tests.**
  - Layer 1, each test named with ADR-0071 and the run's case (11-7, 11-13, 12-1 … 12-16).
  - Ticket 55's tests that pinned the mirroring change to say what Excel does.

## Comments

*(2026-10-01, agent cf-57.)* The engine keeps the line between two cells as the twelfth Windows run
found Excel to. Ticket 55's mirroring is gone.

- **What a cell records.** Each cell records its own four sides, cell over row over column.
  - A border command records the edge on the cells it sets. `BorderChange.Beside` now gives the
    cells beside each outer edge a change that clears their record of it, at whichever level they
    lie: cells, whole rows or whole columns.
  - A copy carries the source's own sides, never a line it shows from a neighbour. `ShownState` is
    renamed `CarriedState` and reads `OwnFormat`, so a paste, Ctrl+D, Ctrl+R and the fill handle write
    only the target's own four sides.
  - `SheetBlock.CellFormatAt` answers those own sides; only its doc comment changed.
- **What is shown.** `Sheet.GetBorders` answers the edge as shown, from either side. Where both
  cells record a line, the upper (left) cell's is shown, else whichever records one.
  `Sheet.GetCellFormat`, and so `ExSheet.CellFormatAt`, answer it too. The internal reads of a
  cell's own record are `OwnSides` and `OwnFormat`, which commands patch and copies carry. No public
  signature changed and no public member was added.
- **Insertion and deletion** move cells with their own records. `JoinInserted`, `TakeLineAcross`
  and `StructuralOutcome.Rejoined` are removed; nothing else used them. Cases 6 to 13 follow from
  moving the records and from the rule.
- **Outlines.** `PlaceInRange.Of` gives whole rows a left edge on column A and no right edge
  (case 14), and the whole Sheet no outer edge (case 15). Over whole rows, each row's cell in column A
  is given a record of its own, because a row's level is the same along the whole row. Over the
  whole Sheet, the rows' Borders are patched as inner sides, and no cell is given a record.
- **Repainting.** A changed top or bottom side now names the row across that edge in
  `SheetChange.Rows`, because the edge shows from both cells. This holds in `FormatCells` and in
  `Restore`, so a command, its undo, a paste and a fill all name it. Ticket 55 got this as a side
  effect of writing the neighbour.
- **The Sheet Document** is unchanged. A document written by ticket 55's code records both sides of
  each edge, and the two agree, so it shows the same edges. The test reads one at every level.

**Found.**
- **Format Cells reads what the cells record, not the edge as shown** (the eleventh run, case 24).
  - Excel draws the inside edge of A1:A2 grey and dotted where A1 records a thick bottom and A2 is
    plain. So `Sheet.GetCellFormats`, which only Format Cells reads, answers each cell's own sides,
    and its doc comment says why.
  - Ticket 52's note on case 24 is answered by this ticket. A1:A2 now opens as Excel opens it.
- **Ticket 49's third box is stale.** It says ticket 55 keeps both sides equal, so the core can draw
  either side's record. After this ticket the two records can differ (after a paste, a fill or a
  deletion), so ExSheet answers the core from `Sheet.GetBorders`.
- **Ticket 49 also needs level changes to repaint one row further.** For a level change,
  `SheetChange.Rows` still names only rows that hold a cell. An edge set on a whole row's top shows
  on the bottom of the row above, so a level's repaint must reach one row past it on each side.

**Readings that remain, each named in its test or here.**
- **A single cell in Format Cells shows its own sides** (`A_thick_bottom_over_a_plain_cell_is_an_edge_that_differs_case_11_24`).
  A2 alone opens with no top under A1's thick bottom. Case 24 saw only the inside edge of A1:A2.
- **A paste leaves the neighbour's own record under the line shown** (case 12-1, C2's blue).
  Excel's COM and pixels show only the edge, so the record underneath is the model's consequence.
- **Inside over whole rows** sets the right of column XFD and leaves column A's left as each cell has
  it. **Inside over the whole Sheet** sets every side, column A's left and XFD's right included. Both
  follow from cases 14 to 16, and neither was observed.
- **A document written by ticket 55** reads the same edges. A later insertion between two cells that
  both record an edge leaves a line on both edges of the inserted rows. That is what the model does
  with two records, and what Excel would do with such a file; it was not observed.
- **An outline over many whole rows** gives that many cells in column A a record each, as Excel
  records A's left on each.

**Left to ticket 49, as the brief said:** ExSheet answering the core's "which line to draw" from
`Sheet.GetBorders` (ADR-0050, item 15). No painting is built here.

**Tests.** Layer 1 names ADR-0071 and the case: 11-7, 11-12, 11-13, 11-15, 11-24, and 12-1 to
12-16. Ticket 55's tests that pinned the mirroring now state what Excel does. Layer 2 adds case 24
to `FormatCellsDraftTests`.
- ExSheet.Engine.Tests: 2122 passed.
- ExSheet.Components.Tests: 415 passed.
- ExGrid.Tests: 857 passed.
- ExGrid.Components: 1094 passed, one skipped.
- ExGrid.MudBlazor.Tests: 88 passed.
