# 08: Undo and Redo show the Sheet their step was taken on

Status: done
Roadmap: ExSheet — Undo shows the Sheet the undone change was made on and selects what it changed.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §3.

- [x] Ctrl+Z and Ctrl+Y show the step's Sheet and select its range, as Excel does
- [x] A Sheet operation's undo shows the Sheet it brings back or the one it leaves
- [x] Layer 2 and layer 3

**Blocked by:** 06.

**Settled while building (2026-10-21):** the engine's `WorkbookStep.Ranges` names the ranges a step
changed: the cells an edit or a paste wrote, a fill's source with what it filled, the ranges a Cell Format
was set on, the whole columns a width was set on, the whole rows or columns inserted or deleted; for cells
named one by one, the smallest range that holds them; none for a Sheet operation. The widths an entry
widens its columns to add no range, and the columns of one resize are selected together. Delete's undo
selects the ranges it was pressed over, not only the cells it cleared, as the component records them with
the step. An undo or a redo selects one range, with the
Focus on its top-left cell, because the grid places one (ADR-0050, item 4): a step over several ranges
that do not make one together selects the first, the one it acted from (for Ctrl+Enter, the range typed
in). Selecting several would need the grid to place several. Another Sheet is shown with the range handed
with its rows (`ShowViewWith(view, range, focus)`), and on the Sheet shown it is placed under its own
version. A Sheet operation's undo or redo shows the step's Sheet while the Workbook holds it — a Sheet
deleted, renamed or moved, or added or copied again — else the Sheet it was done from (the one a Sheet
was added beside or copied from), else the one the Workbook shows in the place of a Sheet deleted again;
it selects nothing, and that Sheet keeps its own Selection. This replaces 06's interim answer, which
showed the Sheet in the place of a Sheet added and then taken away.
