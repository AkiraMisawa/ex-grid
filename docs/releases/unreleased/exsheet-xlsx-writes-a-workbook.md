Product: ExSheet
Kind: Added
ADR: 0330

`ExSheet.Xlsx` now writes a Workbook as it stands to an `.xlsx`: `XlsxWorkbookWriter.WriteAsync` writes every Sheet in order with the shown one shown, constants, each Formula with the Value the Sheet shows (Excel's `_xlfn.`, `_xlws.` and `_xlpm.` prefixes put back, every Formula a dynamic array over the cells it spills into), Cell Formats at cell, row and column level and column widths, and marks the workbook to be calculated in full when Excel opens it. It reads the Workbook whole before it first yields and refuses by name, as an `XlsxSaveException` naming the Sheet, the cell and what it held with nothing written, a Workbook that declares a Linked Table (not written yet), a cell that shows `#CIRC!`, a Formula or text longer than Excel holds, and more cells than `XlsxSaveOptions.MaxCells` (by default the most a file is read with, so a Sheet formatted on many rows and columns cannot write a file that would not open again).
