Product: ExSheet
Kind: Breaking
ADR: 0331

`ExSheet.Engine` is rebuilt around `Workbook`, with no forwarders kept: `new Sheet(culture)` is now `new Workbook(culture)`, whose one Sheet is `Sheet1`, and `new Sheet(culture, name)` is `new Workbook(culture, [name])`; the Sheet is `workbook.Sheets[0]`, read through `GetValue`, `GetDisplay`, `GetCellFormat` and their peers.
