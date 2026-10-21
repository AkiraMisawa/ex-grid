# 05: Linked Tables are written as Excel Tables on Sheets of their own

Status: done

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §6 (Q5, X1).

- [x] Each Linked Table written as an Excel Table on a worksheet named after it, its rows as they stand
- [x] Refused by name: a Linked Table not answered yet, more rows than 1,048,576, a name already a Sheet's
- [x] Layer 1 tests; the Docs Site says the file reopens in Excel, not in ExSheet, until Excel Tables are read; Change Note
      *(the Docs Site has no `.xlsx` page until ticket 06 writes it, and ticket 06 carries the sentence; the package's
      README says it now)*

**Blocked by:** 04.

**Settled while building (2026-10-21):** `XlsxWorkbookWriter` writes each Linked Table after the Sheets, in the order the
tables were declared, as a worksheet of its name holding an Excel Table of its name (`name` and `displayName`): the columns'
names in a header at A1, the rows below as they stand when the call is made (read through the engine's new
`Workbook.LinkedTableColumns`, whole, before the first yield, as ticket 04 reads the Sheets), a number, text (as a shared
string, so a text of digits stays text), a boolean or an Error Value in its cell, and no cell for a blank. The cells count
toward `XlsxSaveOptions.MaxCells`, the header's included. The Sheet shown stays the worksheet shown.

- **The file layer writes the Excel Table.** `XlsxSheetWriter.AddTable(XlsxTable)` writes the table part
  (`xl/tables/tableN.xml`, ids 1 to N across the workbook), the worksheet's relationship to it and its `tableParts`, its
  content type, an AutoFilter over the table and Excel's default style (`TableStyleMedium2`). It refuses a name Excel does
  not give a Table, a name another table or defined name of the workbook has in any case, overlapping tables, a table added
  after its header's row was begun, a header cell that is not its column's name as text, and a sheet that ends with its
  header unwritten. The reader does not read a table part; it keeps `tableParts` among the elements it does not read.
- **A structured reference is written as the engine stores it**, which is Excel's own form: single brackets whatever the
  column's name holds, `'` before each of `[ ] # '` (`Positions[Market Value]`, `Positions[Rate'#]`; TEXT-021 and TEXT-022
  in verification/2026-09-27-windows-excel). The engine reads only `Table[Column]`, so no `[#This Row]`, `@` or column
  range reaches the writer. Ticket 07 asks Excel whether the file form is the one it shows.
- **Refused by name, beyond §6's three** (each by the spine's first rule, as ticket 04 refused `#CIRC!` by §6's reasoning
  for `#GETTING_DATA`; flagged for the user to confirm):
  - a Linked Table of **no rows**: an Excel Table holds at least one, and over one empty row its Formulas answer otherwise
    than ExSheet's (`ROWS` gives 1 where ExSheet gives 0, `COUNTBLANK` 1, the column itself 0 where ExSheet shows
    `#VALUE!`), with nothing on the file saying so;
  - a name **a worksheet cannot have**, since the worksheet is named after the table: longer than 31 characters, holding `\`,
    or `History` (a Linked Table's name may be 255 characters and begin with `\`);
  - more than **16,384 columns**;
  - the rows limit is **1,048,575**, a worksheet's 1,048,576 less the header's row (`XlsxWorkbookWriter.MaxLinkedTableRows`);
  - a Formula that reads, by a structured reference, a table or a column **no Linked Table declares** (the engine shows
    `#NAME?` or `#REF!`; Excel refuses such a Formula on entry, TABLE-011 and TABLE-013);
  - a Formula that reads a column whose name **begins or ends with a space**, which Excel's structured reference does not
    keep, or **holds `@`**, whose form in a file ticket 07 asks Excel (Excel reads `Positions[@PV]` as the row the Formula
    stands in).
- **Opened again by ExSheet** (X1): with a Formula reading the table, the open is refused by name (`ExcelTable`); with
  none, the table's worksheet opens as a Sheet of its cells and the Open Report lists its Excel Table as not carried.
- **Found and fixed:** the engine stored a column whose name holds `@` unescaped (`=SUM(Positions[['@PV]])` was stored as
  `=SUM(Positions[@PV])`), which its own lexer refuses, so such a Formula did not read back from a Sheet Document.
  `FormulaText.WriteStructuredReference` now writes `'@`, as Excel's own form escapes it (`=SUM(Positions['@PV])`); the
  writer still refuses such a column by name until ticket 07 settles its form in a file.
