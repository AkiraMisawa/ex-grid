Product: Data
Kind: Added
ADR: 0330

A new package, `ExGrid.Data.Xlsx`, reads and writes the xlsx file format with nothing but the platform's zip and XML: `XlsxReader` streams a workbook's sheets, shared strings, Formulas (shared and array ones included), column widths, styles and theme colours in batches, yielding and reporting progress, and `XlsxWriter` writes the same model back as a valid package. A file it cannot read faithfully is refused by name as an `XlsxException` naming its reason, part, sheet and cell: not a zip, macro-enabled, binary, legacy, encrypted, a DTD, a part that is not well-formed or cannot be inflated, a cell written twice, out of order or naming a style the file does not hold, or beyond `XlsxReadOptions.MaxUncompressedBytes` (1 GiB) or `MaxCells` (ten million) by default.
