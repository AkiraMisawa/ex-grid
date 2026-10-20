Product: ExSheet
Kind: Added
ADR: 0331

`Workbook.ToDocument()` writes a Workbook Document — every Sheet in order, the Sheet shown (`Workbook.Shown`), the culture and the Linked Tables' declarations, never a Value — and `Workbook.Open(WorkbookDocument.FromJson(json))` opens it again. `WorkbookDocument.FromJson` also reads a Sheet Document of any readable version as a Workbook of that one Sheet, and refuses an unknown version or anything it does not understand by name.
