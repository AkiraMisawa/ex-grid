Product: ExSheet
Kind: Behaviour
ADR: 0331

In a Workbook of several Sheets, a command names the Sheet it acts on: `DoAsync(sheet, edit)` does an edit, a Sheet operation among them, on that Sheet whether it is shown or not, and `CellFormatAt(sheet, address)` reads a cell there. `DoAsync(edit)` and `CellFormatAt(address)`, which name none, are refused with `SheetRefusalReason.SheetNotNamed` once the Workbook holds several Sheets, and work as before while it holds one; a page that adds Sheets from code names the Sheet. The new `SheetsChanged` event hears every Sheet added, deleted, renamed, moved or copied, an undo or redo of one included, with the names before and after.
