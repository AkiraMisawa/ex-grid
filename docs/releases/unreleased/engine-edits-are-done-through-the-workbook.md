Product: ExSheet
Kind: Breaking
ADR: 0331

`sheet.Do(edit)` and `sheet.Check(edit)` are now `workbook.Do(sheet.Name, edit)`, which returns a `WorkbookStep`, and `workbook.Check(sheet.Name, edit)`; `sheet.Enter`, `SetEntries`, `InsertRows`, `DeleteRows`, `InsertColumns`, `DeleteColumns`, `SetNumberFormat`, `SetAlignment`, `SetCellFormat`, `SetColumnWidth` and `SetAutomaticColumnWidth` are `workbook.Do(sheet.Name, …)` with the `SheetEdit` of the same name, and what the edit changed on the Sheet is `step.Change.For(sheet)`.
