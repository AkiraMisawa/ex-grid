# 55: The engine follows the eleventh Windows run

Status: ready-for-agent

**What to build:** the engine's corrections from
[ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "What the
eleventh Windows run settled". Ticket 45 built three readings that Excel answered otherwise. The
record is `verification/2026-10-01-windows-excel-11/cell-format.md`.

**Blocked by:** None (can start immediately)

- [ ] **The line between two cells is one line** (cases 7, 13). Setting or clearing a side also sets
      or clears the neighbour's opposite side, at every level (cell, row, column), so the two records
      never disagree and the later setting wins from either side. An outline sets the edges read
      from the cells outside the range too; "no borders" clears them. A side on the Sheet's outer
      edge has no neighbour. Rename the case-7 tests to say what Excel does.
- [ ] **An inserted row or column takes the Fill, Font, Number Format and Alignment of the one before
      it, and not its Borders** (case 12). Its top (or left) edge is the edge it shares with the one
      before, so it reads that one's bottom (or right) line; nothing else is repeated. Whether the
      Font is copied is a reading (not yet observed).
- [ ] **An outline over whole columns sets only their left and right edges** (case 15): no top of
      row 1, no bottom of row 1048576. Whole rows set only top and bottom, and the whole Sheet sets the
      left of column A and the right of column XFD, as its columns do; both are readings by mirror,
      not observed.
- [ ] The Sheet Document needs no new version: it records the same sides, now kept equal.
- [ ] Layer 1, each test named with ADR-0063 and the run's case.
