# 07: Questions to Excel for `.xlsx`

Status: ready-for-human
Roadmap: internal

**Why:** ADR-0330 builds nothing from reasoning about Excel. In the next Windows run:

- [ ] A file ExSheet saved opens in Excel with no repair prompt, recalculates, and shows the same Values
- [ ] The corpus files' readings (Number Formats, theme colours, widths) match what Excel shows
- [ ] A Linked Table's worksheet opens as an Excel Table whose Formulas answer
- [ ] A structured reference written in the form Excel shows (ticket 05: single brackets, `'` before `[ ] # '`, as in
      `Positions[Total $]` and `Positions[Rate'#]`) is the form a file stores, and opens with no repair prompt; and how Excel
      stores a column whose name holds `@` (ticket 05 refuses one) or begins or ends with a space (refused too)
- [ ] A table column's name holding a line feed (written as XML carries it, `&#xA;`, where Excel writes `_x000a_`) opens
      equal to its header cell's text; and whether a table column's name has a length limit below a cell's 32,767
- [ ] Built-in Number Formats 5 to 8 (currency): how Excel shows them under other locales, with the code the file writes and
      without one (ticket 03 reads them from the code, and refuses one with none)
- [ ] Column widths when the workbook font is not Calibri 11 (a Mac's `baseColWidth="10"`), and whether Excel snaps a width
      ExSheet writes, unsnapped, to whole pixels (ticket 03's `ColumnWidthUnit`)
- [ ] What Excel paints for a solid fill of the automatic colour (ticket 03 opens it as no fill, and lists it)
- [ ] Whether `bestFit` marks a width a typed entry widened, or one fitted by a double-click (AutoFit): ticket 03 reads either
      as widened by entry
- [ ] A `<col>` that records a style at the default width without `customWidth` shows at the default width; and whether a
      `<col style>` with no width at all is a width of 0, as ticket 03 believes
- [ ] A Formula that does not spill, written as ticket 04 writes every Formula (a dynamic array, `t="array"` over its own
      cell with `cm="1"`), opens in Excel 365 as typed, with no braces and no `@`, and recalculates to the same Value; and
      what Excel 2016 shows for it
- [ ] Each function in ticket 04's `_xlfn.` list (`WrittenFormula.Newer`) is computed by Excel, not `#NAME?`; `FILTER` and
      `SORT` with `_xlfn._xlws.`; and whether a function left off the list (`ISO.CEILING`, `CONCATENATE`, `DATEDIF`) needs a
      prefix
- [ ] A `LET` written with `_xlpm.` names and a Spill Range reference written `_xlfn.ANCHORARRAY(A1)` open as typed
- [ ] The spilled cells' Values written as plain cells under a dynamic array's anchor (text as `t="str"`) are taken as the
      spill, not as constants that block it (`#SPILL!`)
- [ ] Whether Excel's 8,192-character limit on a Formula counts the `_xlfn.`, `_xlpm.` and `_xlfn.ANCHORARRAY` text a file
      stores, or only the Formula as shown (ticket 04 counts the Formula as shown)
- [ ] What Excel does with a file whose Formula holds a text constant over 255 characters, calls nested past 64 levels, or a
      call of more than 255 arguments (ticket 04 refuses each): a repair prompt, a refused Formula, or a computed one; and
      whether plain parentheses count toward the 64 levels (ticket 04 counts calls only)
- [ ] Whether Excel reads `=LET(sum,1,SUM(sum,2))`, written `_xlfn.LET(_xlpm.sum,1,SUM(_xlpm.sum,2))`, as a call of `SUM`, as
      ticket 04 and the engine do, or of the bound name
- [ ] How Excel 2016 and LibreOffice read a cell value of `#SPILL!`, `#CALC!` or `#FIELD!` written as an Error Value
      (`t="e"`), which earlier versions do not know
- [ ] Whether an Error Value ticket 04 writes as a cell's Value (`t="e"`, every one but the refused `#CIRC!` and
      `#GETTING_DATA`) opens as that Error Value in Excel 365 before the full calculation replaces it, and in a program that
      does not calculate
- [ ] A cell of no value written where a formatted row crosses a formatted column (ticket 04) shows the row's and the
      column's parts together, as ExSheet shows them

**Blocked by:** 05.
