Product: ExSheet
Kind: Breaking
ADR: 0331

`Sheet.Open(document)` is now `Workbook.Open(document)`, which opens a Sheet Document as a Workbook of that one Sheet, and `sheet.ToDocument()` is `workbook.ToDocument(sheet.Name)`.
