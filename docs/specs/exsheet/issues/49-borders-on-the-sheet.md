# 49: Borders on the Sheet, as Excel draws them

Status: ready-for-agent

**What to build:** the Sheet's Borders, drawn through ADR-0050 item 15, as [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) says.

**Blocked by:** 47 and 57. The eleventh Windows run's group 3 is in `verification/2026-10-01-windows-excel-11/cell-format.md`;
ticket 57 makes the engine keep the line between two cells as Excel does, and ExSheet answers the
core's question of which line to draw by its rule (the upper or left cell's, where both record one).

- [ ] ExSheet answers each cell's Border sides from the engine.
- [ ] A Border on a whole row or column is drawn on cells that hold nothing, so a change to a level
      repaints every painted row it covers, as ticket 48 does for Fills.
- [ ] The line between two cells is one line (case 7; ticket 55 keeps both sides equal), so the core
      can draw either side's record.
- [ ] A Fill covers the gridlines at its cell's edges, as the run observed (cases 4–6).
- [ ] The thirteen line styles match case 9's table at 100% and 150%: every 1-px style on the
      gridline; medium and the medium dashes on the gridline and the pixel above it; thick on the
      gridline and a pixel each side; double as two 1-px lines either side of the gridline with the
      gridline's pixel white; the dash and dot patterns as tabled (dashes 8 px at 100%, 9 at 150%).
- [ ] Inside the Selection, borders stay drawn over its shade; the Selection's outline covers the
      outer ones (case 11).
- [ ] Rows keep their one height where Excel would raise them for a medium or a thick line (ADR-0063).
- [ ] A line on column A's left edge lies under the Row Headings' edge, as Excel draws it (run 12,
      case 14).
- [ ] Layer 3 beside Excel's screenshots, which is Part C of the run (SH-46).
