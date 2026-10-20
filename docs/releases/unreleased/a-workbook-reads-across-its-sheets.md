Product: ExSheet
Kind: Added
ADR: 0331

`ExSheet.Engine` gains `Workbook`: several Sheets in order under one culture, where a Formula on one Sheet reads another's cells by naming it (`=Rates!A1`), and a change on any Sheet recalculates what reads it on every Sheet, the Values published together once the whole recalculation is done. `Workbook.Do(sheet, edit)` names the Sheet an edit is made on and returns a `WorkbookStep` that undoes it; Linked Tables are declared on the Workbook and read from any Sheet. The ExSheet component does not show a Workbook yet, and a Sheet of a Workbook of several is not yet renamed, nor its rows or columns inserted or deleted (`NotSupportedException`).
