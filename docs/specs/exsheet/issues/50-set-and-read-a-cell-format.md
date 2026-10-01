# 50: Set and read a Cell Format

Status: done

**What to build:** the commands of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "The commands".

**Blocked by:** 45

- [x] **`SetCellFormatAsync(CellFormatChange)`** acts over the Selection (SH-44).
  - It sets only the parts the change names, as one undo step.
  - Borders are relative to each selected range.
- [x] `SetNumberFormatAsync` and `SetAlignmentAsync` become shorthands for it, and behave as the
      change they abbreviate.
- [x] **`CellFormatAt(address)`** answers cell over row over column.
- [x] All of them are refused by name while an edit is open (SH-29, SH-43).
- [x] XML doc comments on every public member.
- [x] Layer 2.

## Comments

*(2026-10-01, built.)* The commands of ADR-0063 on the `ExSheet` component.

- **`Task<bool> SetCellFormatAsync(CellFormatChange change)`** sets the parts the change names on
  every range of the Selection, as one engine step and one undo step, through the engine's
  `SheetEdit.SetCellFormat`. Its Borders are relative to each range, so each range gets its own
  outline. It answers false when nothing is selected, as `SetNumberFormatAsync` did.
- **`SetNumberFormatAsync` and `SetAlignmentAsync`** are now one line each: they build the change
  they abbreviate (`NumberFormat`, with null as General; `Alignment`) and call
  `SetCellFormatAsync`. They act, refuse and answer as that change does, by construction. Their
  existing tests pass unchanged.
- **`CellFormat CellFormatAt(CellAddress address)`** answers `Sheet.GetCellFormat`, cell over row
  over column.
  - It is **synchronous**, as the component's other read of the Sheet, `ToDocument()`, is. The
    `Async` members are the ones that change the Sheet and run on the renderer's context.
  - It takes the engine's **`CellAddress`**, as `SheetEdit` and the rest of the component's
    public surface do; a toolbar turns the grid's Focus into one with
    `new CellAddress(focus.Row, focus.Column)`, as the Name Box does.
  - It is not refused while an edit is open: it is a read, and the open edit has changed nothing.
- **While an edit is open** `SetCellFormatAsync` is refused with `SheetRefusalReason.EditIsOpen`
  and changes nothing (SH-29, SH-43), as the shorthands are. `IsEditing`'s documentation names it.
- **A change the caller got wrong is an argument error, thrown at the call**, before the open edit
  or the Selection is looked at: null (`ArgumentNullException`), a change that names no part
  (`ArgumentException`, the engine's own rule for `SheetEdit.SetCellFormat`), and an alignment that
  is not one (`ArgumentOutOfRangeException`). Before, `SetAlignmentAsync` with an undefined
  alignment answered false when nothing was selected and was refused while an edit was open; it now
  throws in every state.
- **For ticket 52:** OK with nothing touched builds a change that names nothing, which
  `SetCellFormatAsync` throws on. Format Cells should not call it then.
- **For ticket 54:** `CellFormatAt` answers what is recorded; the colour a Number Format's section
  names (ticket 46) is display, carried by `CellDisplay.Colour`, and is not part of it.
- The package's README shows `SetCellFormatAsync` and `CellFormatAt`.
- **Tests:** 20 new cases (layer 2). `CellFormatCommandTests` holds 18: each part on its own (a
  theory of 10), parts that differ cell by cell, one undo step over several ranges, an outline per
  range over a Selection of two ranges, the shorthands against the change they abbreviate (a theory
  of 3), the argument errors, and `CellFormatAt` cell over row over column.
  `RefusedWhileEditingTests` gains `SetCellFormatAsync_is_refused` and
  `CellFormatAt_answers_while_an_edit_is_open`. Each is named with ADR-0063 and SH-43 or SH-44.
  Formatting only the Selection's first range fails 5 of them. ExSheet.Components.Tests 310,
  ExSheet.Engine.Tests 2081, ExGrid.Tests 826, ExGrid.Components 1062 (one skipped), and
  ExGrid.MudBlazor.Tests 88, all passing.
