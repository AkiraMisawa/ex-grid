# Part E — ExPivot's readings beside Excel (`docs/specs/expivot/excel-behaviours.md`, ticket 07)

Run 2026-10-12, 09:38 to 09:55, at the verified commit `8436e45e`. One row per item of `excel-behaviours.md`. **Nothing
was decided**: a reading marked contradicted is recorded, not corrected.

**How.** For each layout, a CSV of its own (`part-e/csv/`, from `part-e/items.json`), written once and given to both:

- **ExPivot**: `/pivot-csv` on the WebAssembly host, Chrome 154 headed on Windows, through "Choose any CSV" (a file nobody
  has described): the suggested Schema confirmed as suggested, then the layout built in the Field List with Playwright's
  own input (`part-e/pivot-probe.mjs`): each field dragged onto its Area (a first drag after the file was read sometimes
  did not land and was repeated, which the record notes), each Value Field's Aggregation and Show Values As set in Value
  Field Settings…, an Item hidden through Filter…, the form chosen in the Layout menu. The report read from the DOM,
  every row by `aria-rowindex` and cell by `aria-colindex` (`part-e/records/expivot.json`), and a picture of the pivot
  (`part-e/shots/<id>-expivot.png`). This Part asks what the report says; how the OS's keys reach a page is Parts C, D,
  F and G's.
- **Excel**: the same file opened in an Excel of the script's own (`part-e/part-e-excel.ps1`) with `Workbooks.Open` as
  the user's regional format reads it (en-GB), a PivotCache over its used range and a PivotTable at A3 of a new sheet,
  built through COM as `excel-behaviours.md` says: the Value Fields with `AddDataField` and their Function and
  Calculation, then the row and column fields' Orientation in order, a hidden Item by `PivotItem.Visible`, the form by
  `RowAxisLayout`. TableRange2 read cell by cell (Text, Value2, NumberFormat), the text read again with every column 40
  characters wide, and a picture (`part-e/shots/<id>-excel.png`). Records: `part-e/records/excel-wide.json`.
  - *A first pass set the row fields before the Value Fields, and `AddDataField` on Region, already a row field, took it
    out of the rows (item 3's first table had no rows: `part-e/records/excel-first.json`). The pass of record adds the
    Value Fields first, as a field dragged into both Areas stays in both in Excel's pane.*
- **Compared** (`part-e/compare.py`, the whole of it in `part-e/expivot-cells.md`): the body cell by cell by row label,
  as the text each shows, and where the text differs, the number ExPivot shows against Excel's Value2 to 15 significant
  digits ("the same value, shown differently").

## The twelve readings that gate 0.1.0 (ADR-0202)

| Item | Reading | Excel | ExPivot | Marked |
|---|---|---|---|---|
| 2 | Text Items ignore case; the first spelling labels the Item | `East` 60 (East, east, EAST), `West` 5, Grand Total 65 | the same | **observed** |
| 3 | A missing value is `(blank)`, an Item like any other, counted by Count | `(blank)`: Count of Region empty, Count of Amount 2, Sum 21; Grand Total 2, 4, 36 | the same, every cell | **observed** |
| 4 | Count counts every non-empty value, text included; Count Numbers counts numbers only | Count of Value: A 2, B 2, total 4. Count Numbers (Excel names it `Count of Value2`): **A 1, B 2, total 3** | Count: the same. Count Numbers (`Count Numbers of Value`): **A 0, B 0, total 0** — the column holds `x`, so the suggested Schema read it as Text, and a Text field holds no numbers | **contradicted** for Count Numbers (Count observed) |
| 5 (a) | Average, StdDev and Var over no numbers are `#DIV/0!` | **an empty cell** in each (group A, whose Amounts are blank) | **an empty cell** in each | **contradicted**: both show an empty cell, not `#DIV/0!` |
| 5 (b) | StdDev and Var over one number are `#DIV/0!`; StdDevp and Varp over one number are 0 | `#DIV/0!`, `#DIV/0!`, 0, 0 (group B) | the same | **observed** |
| 6 (a) | Product where there are no values: an empty cell | empty (group A) | empty | **observed** |
| 6 (b) | Product over values none of which is a number: 0 | 0, 0, total 0 (a text column) | the same | **observed** |
| 7 | An empty cell stays empty, never 0 | East × Pear and West × Apple empty | the same | **observed** |
| 8 | A subtotal and a grand total are aggregated from the records | East's Average 4 (not the average of D1's 2 and D2's 10), Count 4; Grand Total Average 23.2, Count 5 | the same, every cell | **observed** |
| 15 | Show Values As: % of Grand Total, % of Column Total, % of Row Total, in `0.00%`; a total's share of itself 100.00% | each in `0.00%` (the cells' NumberFormat), totals `100.00%` | the same text in every cell, all three | **observed** |
| 16 | General writes at most 15 significant digits, as a cell does | **General shows at most eleven characters**, at the PivotTable's width and at 40 characters alike: `1.414213562`, `1.23457E+11`, `1.23456789`, `1.23457E-05`. And **Excel read the CSV's numbers to 15 significant digits by truncation**: `123456789012.345678` became `123456789012.345`, `1.23456789012345678` became `1.23456789012345` | 15 significant digits: `1.4142135623731`, `123456789012.346`, `1.23456789012346`, `1.23456789012346E-05`, the CSV's full doubles rounded | **contradicted**: Excel's General is eleven characters, not fifteen digits; the values in B, C and D differ in their fifteenth digit (Excel's truncated input) |
| 17 | Hidden Items are left out of the totals | North hidden: Grand Total 30 | the same | **observed** |

Items 5 (b), 8 and 15's other cells read the same value both sides, shown with Excel's eleven characters (item 16).

## The other items

| Item | Marked | What was read |
|---|---|---|
| 1 | **observed** (for text) | `Apple`, `banana`, `cherry`, `(blank)` last, in both; `Banana` folded into `banana` (its first spelling). ExPivot keeps one type per field, so numbers, dates, Booleans and errors in one field were not asked |
| 9 | **observed** | Compact: one label column `Row Labels`, Desk indented under Region, the subtotal on the group's own row; Outline: `Region` and `Desk` columns, the subtotal on the group's row; Tabular: the subtotal row at the bottom, `East Total`, `West Total`. The same in both (ExPivot's outer labels carry their collapse button, `−`). Compact was the default in both: ExPivot offers "Show in Compact Form" disabled there, as item 23 reads |
| 10 | not run | Subtotals' default position and the greyed choice: not asked in this pass |
| 11 | not run | A collapsed Item: not asked in this pass |
| 12 | **observed**, with a layout difference | `FALSE`/`TRUE` bands over `Buy`/`Sell`, `FALSE Total`, `TRUE Total`, `Grand Total` on the right, the same values. **Excel also shows a header row `Sum of Amount` / `Column Labels`, which ExPivot does not draw** |
| 13 | **contradicted** in wording | Σ Values in Columns, innermost, `Sum of Amount`/`Sum of Qty` under each column Item, in both. **Excel captions the grand total columns `Total Sum of Amount` and `Total Sum of Qty`; ExPivot draws a `Grand Total` band with `Sum of Amount`/`Sum of Qty` under it**; and Excel's `Column Labels` corner row, as item 12 |
| 14 | **observed** | `Sum of Amount`, `Count of Region`, `Sum of Amount2` in both. (Not asked: the refusal "PivotTable field name already exists.") |
| 18 to 25 | not run | Sort by value, and the Field List's gestures (ticking, unticking, dragging, Σ Values, the field's menu, the report filter band, a double click): Excel's half needs its Field List pane driven by hand; not asked in this pass |
| 26 | not run | Date parts: not asked in this pass |
| 27 | not run | The Japanese edition's words: Excel's display language here is English (UK). Asking the user whether to add Japanese is left to the end of the run |
| 28 | not run | The Design tab's Layout names: not asked in this pass |

## The differences, and what they belong to

1. **Item 4, Count Numbers over a column holding text**: Excel counts the numeric cells (1, 2, 3); ExPivot answers 0,
   having read the column as Text (the suggested Schema). It belongs to ADR-0060's Count Numbers, and to item 19's
   note that ExPivot decides by the declared type where Excel decides by the data. Excel names the field `Count of
   Value2` where ExPivot names it `Count Numbers of Value` (item 14's captions).
2. **Item 5 (a)**: over no numbers, Average, StdDev and Var are an empty cell in Excel and in ExPivot; the reading says
   `#DIV/0!` (ADR-0060).
3. **Item 16**: Excel's General shows eleven characters at most, whatever the column's width; ExPivot's shows 15
   significant digits. And Excel truncates a CSV's number to 15 significant digits when it opens the file, where
   ExPivot's CSV reader keeps the double and rounds it for display, so the values in the fifteenth digit differ
   (ADR-0060, ADR-0064).
4. **Items 12 and 13**: Excel draws a `Sum of Amount` / `Column Labels` corner row over column Items; ExPivot does not.
   With two Value Fields, Excel's grand total columns are `Total Sum of Amount`, `Total Sum of Qty`; ExPivot's are a
   `Grand Total` band over `Sum of Amount`, `Sum of Qty` (ADR-0060, a layout and wording difference).
