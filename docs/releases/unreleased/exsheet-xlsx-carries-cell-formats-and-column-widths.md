Product: ExSheet
Kind: Added
ADR: 0330

`ExSheet.Xlsx` now carries a file's Cell Formats at cell, row and column level (Number Formats, horizontal alignment, font colour, bold, italic, underline, strikethrough, solid fills, ADR-0071's borders) and its column widths in Excel's unit, theme colours resolved to RGB. A Number Format the engine does not read, such as `[h]:mm`, refuses the file as `XlsxOpenRefusal.NumberFormat`, and what is not carried (the theme link, typefaces and sizes, vertical alignment and wrapping, other alignments and underlines, pattern fills, diagonal borders, a Sheet's default width) is listed in the Open Report under new `NotCarriedKind` members; `ExGrid.Data.Xlsx` gains `XlsxRow`, `XlsxSheetWriter.WriteRow` and a column's `BestFit`, and `ExSheet.Engine` gains `NumberFormat.TryParseAsWritten`.
