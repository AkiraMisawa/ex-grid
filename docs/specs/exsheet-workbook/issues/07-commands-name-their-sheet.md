# 07: Commands name their Sheet, and the application manages Sheets

Status: done
Roadmap: ExSheet — An application's commands name the Sheet they act on, and it can add, rename, move, copy and delete Sheets itself.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §6.

- [x] Commands and addresses take a Sheet; one naming none is refused by name while there are several Sheets
- [x] Commands for each Sheet operation, and an event for each change; all on the one undo stack
- [x] Layer 2 tests; Docs Site and Change Note

**Blocked by:** 03, 05.

**Settled (2026-10-20):** `DoAsync(sheet, edit)` and `CellFormatAt(sheet, address)` name the Sheet; the
ones naming none are refused with `SheetRefusalReason.SheetNotNamed` while there are several Sheets. The
Sheet operations are the engine's `SheetEdit`s (ticket 03) done through `DoAsync(sheet, …)`, and
`SheetsChanged` hears each change with the names before and after. A command that acts on the Selection
acts on the Sheet shown, where the Selection is, read with it when the command runs (ADR-0331 §6, as built).
