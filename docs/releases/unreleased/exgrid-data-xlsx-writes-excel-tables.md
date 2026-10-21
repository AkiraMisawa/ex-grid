Product: Data
Kind: Added
ADR: 0330

`ExGrid.Data.Xlsx` writes an Excel Table: `XlsxSheetWriter.AddTable(XlsxTable)` adds one over a sheet's cells before its header's row, with its table part, the sheet's relationship and `tableParts`, its content type, an AutoFilter and Excel's default table style. The writer refuses a name Excel does not give a Table, one another table or defined name of the workbook has, overlapping tables, and a header whose cells do not hold the columns' names as text.
