# 49: Borders on the Sheet, as Excel draws them

Status: ready-for-agent

**What to build:** the Sheet's Borders, drawn through ADR-0050 item 15, as [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) says.

**Blocked by:** 44, 45, 47, and the eleventh Windows run's Part A (group 3)

- [ ] ExSheet answers each cell's Border sides from the engine.
- [ ] Where both sides of an edge are recorded, ExSheet answers with the line Excel draws (run 11,
      case 7).
- [ ] A Fill covers the gridlines at its cell's edges, as the run observed (cases 4–6).
- [ ] The thirteen line styles match the run's crops at 100% and 150% (case 9), and a thick line
      lies as case 8 measured.
- [ ] Layer 3 beside Excel's screenshots, which is Part C of the run (SH-46).
