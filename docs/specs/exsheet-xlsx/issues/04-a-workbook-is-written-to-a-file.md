# 04: A Workbook is written to a file

Status: done

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §6.

- [x] Written from the Workbook as it stands: every Sheet, each Formula with its Value, prefixes put back, Cell Formats and widths
- [x] Marked for full calculation on load
- [x] The cost of formatted rows crossing formatted columns bounded or settled: ticket 03's `SheetFormatPlan` writes a cell
      wherever a formatted row crosses a formatted column with a format of its own, so a Sheet with many of each writes
      rows × columns cells, unbounded today
- [x] Open, save, open again keeps everything §4 carries
- [x] Layer 1 tests; Change Note

**Blocked by:** 03.

**Settled while building (2026-10-21):** `XlsxWorkbookWriter.WriteAsync(workbook, stream, options, token)` writes a
`Workbook` (not a Workbook Document, which holds no Values) through the file layer's `XlsxWriter`: every Sheet in order under
its name, the Sheet shown as the active tab, `calcPr fullCalcOnLoad="1"`, the 1900 date system, constants, each Formula with
the Value the Sheet shows (`RAND()`'s included), the cells an array spills into with their Values, ticket 03's formats and
widths. A refusal is an `XlsxSaveException` with an `XlsxSaveRefusal` reason, the Sheet, the cell and what it held, and
nothing reaches the stream.

- **Read whole before the first yield.** The Workbook is read, and the package built in memory (compressed), synchronously;
  only the hand-off to the stream is awaited. So the file is the Workbook as it stood when the call was made, whatever an
  input does to it afterwards (the spine's sixth rule), and a refusal met anywhere leaves the stream untouched. Ticket 06
  takes the call at its place in the Input Order.
- **Every Formula is a dynamic array.** Each is written `t="array"` over its Spill Range (or its own cell) with the cell
  metadata Excel 365 writes (`cm="1"`, `xl/metadata.xml`). The engine computes every Formula as Excel 365 does (ADR-0125); a
  Formula written plainly is read by Excel as one of an earlier version, which implicitly intersects a range an operator or
  an argument takes (`=SUM(A1:A3*2)` as `=SUM(@A1:A3*2)`), and its Value would change on the full calculation. Ticket 07
  asks Excel how it shows one that does not spill.
- **Prefixes.** `WrittenFormula` splits the engine's invariant text as the engine's lexer does and puts back `_xlfn.` before
  every Excel function newer than 2007 (declared by the engine or not, so a function ExSheet does not support is computed by
  Excel), `_xlfn._xlws.` before `FILTER` and `SORT`, `_xlpm.` before each name a `LET` binds or a `LAMBDA` takes where it is
  bound, and `A1#` as `_xlfn.ANCHORARRAY(A1)`. The names it leaves unbound are checked against `Entry.UnboundNames`; where
  they differ (`=LET(x,x+1,x)`, whose inner `x` the engine leaves unbound) the Formula is refused rather than prefixed in the
  wrong place. Text in quotes and Sheet names are never touched. Ticket 07 asks Excel to confirm the list.
- **Text** goes into the shared strings, as Excel writes it; a Formula's text result is written in its cell (`t="str"`).
- **Refused by name:** a Workbook that declares a Linked Table (`LinkedTable`; ticket 05 writes it as an Excel Table and
  narrows this to the refusals ADR-0330 §6 lists), a cell that shows `#CIRC!` (`CircularReference`: a file holds Excel's Error
  Values only, Excel shows 0 there, and ExSheet refuses to, ADR-0047, so no Value the file can hold is the one shown — the
  same reasoning as §6's `#GETTING_DATA`), a Formula longer than Excel's 8,192 characters, one that reads a Sheet the
  Workbook does not have (Excel would read it as another workbook's) or one whose names bind otherwise (`Formula`), text longer than 32,767 characters or holding half of a surrogate pair, and a Sheet name with a control
  character (`Text`), and more cells than `XlsxSaveOptions.MaxCells` (`TooManyCells`).
- **The crossings are bounded by the read limit.** A cell element is a cell (the Sheet's own, the cells an array spills into,
  and the cells of no value at crossings), and the default `MaxCells` is `XlsxReadOptions.DefaultMaxCells`, so whatever is
  written opens again under the reader's defaults; an application that raises one may raise the other. `SheetFormatPlan` now
  counts the crossings from the runs before it builds anything: a row run leaves a mask of parts to its columns, a column
  run records a mask of parts other than the default's, and the row crosses the column where the masks meet, so the count
  costs rows runs plus 32 × column runs, never rows × columns. A Sheet formatted on every row and every column
  (17,179,869,184 crossings) is refused at once by name. Within the limit, the crossings are kept per row run as column
  ranges and written row by row, never held as a cell each.
- **A code of its own that spells the Workbook culture's localised built-in** (`m\/d\/yyyy` under en-US) is that built-in
  once a Workbook opens it (the engine's `OpenedIn`, as Excel records it), and is written as its id. Ticket 03's round trips
  of a file under en-US keep it: what a file stored as its own code reads as its own code, unless the culture spells the
  built-in so.
- `tests/ExGrid.PackageSmoke/XlsxRoundTrip.cs` saves the opened workbook through the packed writer and opens it again as the
  same document.
- **Found, not fixed:** the engine's evaluator recurses once per binary operator, so a Formula of about four thousand
  chained operators (`=1+1+…`) overflows the stack of the process that recalculates it. It is the engine's, and is met before
  any file is written; the writer's tests use a long Formula of shallow depth.
- No Docs Site page: the site's `.xlsx` page and Example come with ticket 06, when ExSheet opens and saves; nothing a
  Consumer of the components sees changes here.
