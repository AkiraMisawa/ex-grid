Product: ExSheet
Kind: Added
ADR: 0330

A new package, `ExSheet.Xlsx`, reads an `.xlsx` into a Workbook Document under the culture the application declares: `XlsxWorkbookReader.ReadAsync` makes every worksheet a Sheet in order with the shown one shown, carries constants and Formulas (Excel's `_xlfn.`, `_xlws.` and `_xlpm.` prefixes taken off, shared formulas expanded, a spilled array as its anchor's Formula, 1904 date serials moved to 1900's), and returns an Open Report of what was not carried and of the cells naming functions ExSheet does not support. A Formula the engine cannot read, a reference to a defined name, another workbook or an Excel Table, and a legacy array formula are refused as an `XlsxOpenException` naming the Sheet, the cell and what it held. For readers like it, `ExSheet.Engine` gains public constructors for `SheetDocument` and `WorkbookDocument`, and `Entry.MovedBy`, `Entry.UndeclaredFunctions` and `Entry.UnboundNames`.
