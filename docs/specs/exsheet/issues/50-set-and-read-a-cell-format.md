# 50: Set and read a Cell Format

Status: ready-for-agent

**What to build:** the commands of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "The commands".

**Blocked by:** 45

- [ ] **`SetCellFormatAsync(CellFormatChange)`** acts over the Selection (SH-44).
  - It sets only the parts the change names, as one undo step.
  - Borders are relative to each selected range.
- [ ] `SetNumberFormatAsync` and `SetAlignmentAsync` become shorthands for it, and behave as the
      change they abbreviate.
- [ ] **`CellFormatAt(address)`** answers cell over row over column.
- [ ] All of them are refused by name while an edit is open (SH-29, SH-43).
- [ ] XML doc comments on every public member.
- [ ] Layer 2.
