# 55: The engine follows the eleventh Windows run

Status: done

**What to build:** the engine's corrections from
[ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "What the
eleventh Windows run settled". Ticket 45 built three readings that Excel answered otherwise. The
record is `verification/2026-10-01-windows-excel-11/cell-format.md`.

**Blocked by:** None (can start immediately)

- [x] **The line between two cells is one line** (cases 7, 13). Setting or clearing a side also sets
      or clears the neighbour's opposite side, at every level (cell, row, column), so the two records
      never disagree and the later setting wins from either side. An outline sets the edges read
      from the cells outside the range too; "no borders" clears them. A side on the Sheet's outer
      edge has no neighbour. Rename the case-7 tests to say what Excel does.
- [x] **An inserted row or column takes the Fill, Font, Number Format and Alignment of the one before
      it, and not its Borders** (case 12). Its top (or left) edge is the edge it shares with the one
      before, so it reads that one's bottom (or right) line; nothing else is repeated. Whether the
      Font is copied is a reading (not yet observed).
- [x] **An outline over whole columns sets only their left and right edges** (case 15): no top of
      row 1, no bottom of row 1048576. Whole rows set only top and bottom, and the whole Sheet sets the
      left of column A and the right of column XFD, as its columns do; both are readings by mirror,
      not observed.
- [x] The Sheet Document needs no new version: it records the same sides, now kept equal.
- [x] Layer 1, each test named with ADR-0071 and the run's case.

## Comments

*(2026-10-01, built.)* The engine does what Excel did in the eleventh Windows run, cases 7, 12, 13
and 15. The Sheet Document is unchanged: it records the same sides, now kept equal.

- **One line between two cells (cases 7 and 13).** `ApplyCellFormat` sets a change on the range,
  then sets the Borders alone on the cells beside each outer edge the change sets. `BorderChange.Beside`
  names those cells: the row above with `Bottom`, the row below with `Top`, the column to the left
  with `Right`, the column to the right with `Left`. They are set through the same path as the
  range, so they land at whichever level they lie on: cells, whole rows or whole columns. An edge
  on the Sheet's outer edge has no cell beside it. That is also why whole columns get nothing above
  or below, and whole rows nothing to either side. One undo step puts back both sides: the parts
  are combined into one outcome, with the levels as they were before the first part and each cell
  as it was before the first part that touched it.
- **Case 15.** `PlaceInRange.Of` gives whole columns no top or bottom edge and whole rows no left or
  right edge, and the whole Sheet counts as its columns. `CellsGivenTheirOwn` no longer gives the
  first and last cells of whole columns (or rows) a record of their own. An outline over `B:B`
  records left and right on column B, the right on column A and the left on column C, and no cell.
  Over the whole Sheet, a row that records Borders is patched as an inner row.
- **Case 12.** An inserted row (or column) takes the Number Format, Alignment, Font and Fill of the
  one before it, both its cells' and its level's, and none of its Borders. `JoinInserted` then makes
  the two new edges one line each:
  - The first inserted row takes on its top the line the row above shows on its bottom.
  - The row that moved down takes on its top what the last inserted row shows on its bottom: no
    line, or a column's.
  - Each edge is set on the whole row where only the levels decide it, and cell by cell where a
    cell or a crossing level records Borders.
  - The moved row's cells are kept, as they were, in `StructuralOutcome.Rejoined`, so undo puts
    them back exactly.
- **Renamed tests.** The case-7 test is `The_line_between_two_cells_is_one_line_case_7`. The `None`
  test is `No_borders_clears_the_edges_the_cells_outside_read_case_13`. The case-15 tests and the
  case-12 tests say what Excel does.
- **Tests changed because of the change.** Every test that counted the records, or read the cell
  beside a border, now expects that cell's side too: `CellFormatDocumentTests` (two),
  `Cell_over_row_over_column`, `Delete_keeps_them`, and `Each_selected_range_gets_its_own_outline`
  in `tests/ExSheet.Components`.
- **Public shapes:** unchanged. `CellFormatChange`, `BorderChange`, `CellBorders` and the
  `InsertRows`/`InsertColumns` signatures are as they were; only their doc comments changed.
- **Readings that remain**, each named in its test:
  - An inserted row takes the Font of the row above, as it takes the Fill (case 12 did not look).
  - Whole rows' outline sets only their top and bottom edges. The whole Sheet's sets only the left
    of column A and the right of column XFD. Both are readings by mirror of case 15.
  - Of several inserted rows, only the first reads the line of the row above. The edges between the
    inserted rows are empty.
  - Rows inserted at the top take nothing, so the row that moved down loses the line on its top, the
    Sheet's old top edge. Columns inserted at A are the same.
  - An inserted column follows case 12 by mirror.
  - Inside horizontal over whole columns is recorded on the column, so it also shows on the top of
    row 1 and the bottom of row 1048576. A level is the same the whole length of its column. Ticket
    45 gave those two cells records of their own that kept the Sheet's edge. Inside vertical over
    whole rows is the mirror.
- **Not covered by this ticket, and left as they were:** a paste (`SheetBlock`), Ctrl+D, Ctrl+R,
  the fill handle and a deletion still write each cell's own sides only. After one of them, a cell
  and its neighbour can record different lines on the edge they share, and so can a document that
  ticket 45's code wrote. Case 7 says the later line is drawn, but two records do not say which
  came later. Whether those operations should also make the edge one line, and how the painter
  (ticket 49) draws two records that differ, is a question for the next run or a decision.
- **Tests:** layer 1, 13 new or rewritten cases in `CellFormatTests` and `CellFormatCarryTests`,
  and 3 more `InlineData` rows on the undo theory. Each is named with ADR-0071 and its case.
  - ExSheet.Engine.Tests: 2091 passed.
  - ExSheet.Components.Tests: 310 passed.
  - ExGrid.Tests: 826 passed.
  - ExGrid.Components: 1062 passed, one skipped.
  - ExGrid.MudBlazor.Tests: 88 passed.
