# 58: The formatting keys widen a standard-width column, and localise the date and time built-ins

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"What the twelfth Windows run settled", cases 17 to 19
(`verification/2026-10-01-windows-excel-12/cell-format-12.md`).

**Blocked by:** None (can start immediately)

- [ ] **Widening** (case 17).
  - A formatting key widens a column still at the standard width when a cell's formatted text no
    longer fits. The width is the one General's fitting and the `####` decision use (ADR-0047).
  - The column then leaves the standard width, and is recorded as a column widened by an entry is
    (ADR-0046, SH-26).
  - A column the user has sized never widens (case 18).
  - Format Cells' OK and `SetCellFormatAsync` do the same. That is a reading, not yet observed.
  - The widening is part of the key's one undo step.
- [ ] **The date key** writes built-in 15 (`d-mmm-yy`), localised by the Sheet's culture.
  - en-GB shows `05-Jan-26`, en-US `5-Jan-26`, and ja-JP `05-1-26` (the month as a number).
- [ ] **The time key** writes built-in 20 (`h:mm`), localised: en-GB shows `09:05`, ja-JP `9:05`.
  - Under en-US it writes the AM/PM built-in, which shows `9:05 AM`.
- [ ] **Recording**: both are recorded as culture-localised built-ins, as the short date and the
      currency key's format are (ticket 51).
- [ ] **Tests**: layer 1 for the localised built-ins; layer 2 for the widening and its undo. Name
      each with ADR-0063 and case 17, 18 or 19.
