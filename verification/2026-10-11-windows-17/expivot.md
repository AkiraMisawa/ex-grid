# Part E — ExPivot's readings beside Excel (`docs/specs/expivot/excel-behaviours.md`, ticket 07)

Run 2026-10-12, 09:38 to 09:55 (item 27: 10:40 to 10:59), at the verified commit `8436e45e`. One row per item of `excel-behaviours.md`. **Nothing
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
| 27 | **observed** | Excel's Japanese edition, read from its own screens (`part-e/item27/`, below): `行ラベル`, `列ラベル`, `総計`, `東 集計` (Tabular's subtotal), `合計 / 金額`, `データの個数 / 地域`, `(空白)`, `(すべて)`, `(複数のアイテム)` (the report filter, D2 hidden), `値` (the Values field's caption), and the pane `ピボットテーブルのフィールド`. Every one is the word ExPivot's `PivotWords.Japanese` gives for it, except `列ラベル`, which ExPivot does not paint: it draws no corner row (items 12 and 13) |
| 28 | not run | The Design tab's Layout names: not asked in this pass |

## Item 27, Excel's Japanese edition (run 2026-10-12, 10:40 to 10:59)

**How Excel was put in Japanese.** The user agreed to add Office's Japanese display language. Each step and its record
is in `part-e/item27/`, with its scripts in `scripts/` and its logs in `logs/`.

1. **The Click-to-Run client's own scenario failed.** `OfficeClickToRun.exe scenario=install … productstoadd=LanguagePack.16_ja-jp_x-none culture=ja-jp`
   was run elevated, the user approving UAC, after two prompts came back cancelled. It showed "Microsoft 365 と Office
   のダウンロード中に問題が発生しました", error 30038-25.
   - That call also rewrote `HKLM\SOFTWARE\Microsoft\Office\ClickToRun\Configuration\ClientCulture` from `en-gb` to
     `ja-jp`. It was put back to `en-gb` at 10:58 (`logs/ui-policy-restore.log`).
2. **Microsoft's Japanese language accessory pack installed.** Its setup came from Microsoft's own download
   (`c2rsetup.officeapps.live.com`, signed by Microsoft Corporation, 16.0.20430.20146). It ran elevated, 10:42:23 to
   10:43:38. It installed `root\Office16\1041`: `XLINTL32.DLL` 16.0.20430.20032, Japanese.
3. **Three ways that left Excel in English.** After each, Excel started with display language 2057 and showed English
   words (`tries/`):
   - `HKCU\…\Common\LanguageResources\UILanguageTag` set to `ja-jp`: Excel wrote it back to `en-GB` as it started;
   - the policy `HKCU\Software\Policies\Microsoft\Office\16.0\Common\LanguageResources\UILanguage = 1041`, set
     elevated;
   - `UILanguageTag = ja-jp` with `FollowSystemUI = Off`.
4. **Excel's own Options dialog changed it.** File > Options > Language > Office display language, driven with UI
   Automation and real keys (`scripts/office-ui-lang.ps1`; the user asked for it to be done without them):
   - before: `1. Match Microsoft Windows [English] <preferred>`, `2. English`, `3. Japanese [日本語]`;
   - Japanese selected, Set as Preferred, OK. Excel said "Please restart Office so that your language changes can take
     effect." The next Excel started in 1041.
5. **Put back the same way.** `Microsoft Windows と同じ [英語 (English)]` was set as preferred, and `English` moved above
   `日本語`. The list reads as it did before, Excel started in 2057, and `UILanguageTag` reads `en-GB` again. The policy
   value was deleted, and its `LanguageResources` key with it.
   - Empty keys `HKCU\Software\Policies\Microsoft\Office\16.0\Common` are left; they hold no value.
   - The Japanese language pack stays installed.
   - Whether Office copied the display language to the user's Microsoft account meanwhile was not seen.
   - Twice, after OK, Excel's restart notice was not found in time and the script ended its own blank workbook's Excel
     (`Stop-Process`). Nothing was lost.

**What Excel's Japanese edition showed** (`item27-excel.json`, pictures `item27-*-excel.png`). The CSV `item27.csv` has
fields 地域, 商品, デスク, 金額, with a blank 地域 in one record. Excel opened it under the en-GB regional format,
display language 1041.

- **Compact form.** Rows 地域, columns 商品, values Sum of 金額 and Count of 地域, report filter デスク:
  - the filter `デスク | (すべて)`;
  - `列ラベル` over the column Items;
  - `行ラベル`, `合計 / 金額`, `データの個数 / 地域`;
  - the Items `東`, `西`, `(空白)`, and `総計`;
  - the grand total columns `全体の 合計 / 金額` and `全体の データの個数 / 地域`.
  - The PivotTable's own properties read `CompactLayoutRowHeader 行ラベル`, `CompactLayoutColumnHeader 列ラベル`,
    `GrandTotalName 総計`, and `DataPivotField.Caption 値`. Through COM the blank Item's `Caption` is `(blank)`; the cell
    shows `(空白)`.
- **With D2 hidden** in the filter: `(複数のアイテム)`.
- **Tabular form.** Rows 地域 and デスク: `東 集計`, `西 集計`, `(空白) 集計`, `総計`.
- **The Field List's pane** through UI Automation: `ピボットテーブルのフィールド`, `レポートに追加するフィールドを選択してください`,
  `次のボックス間でフィールドをドラッグしてください`, and the Areas `フィルター`, `列`, `行`, `値`.

**ExPivot with `PivotWords.Japanese`.** `/pivot?words=ja` on the WebAssembly host, Chrome 154 (`item27-expivot.json`,
`item27-expivot.png`):

- `行ラベル` and `総計`;
- the report filter `Currency (すべて)`;
- `ピボットテーブルのフィールド`, `レポートに追加するフィールドを選択してください:`, `次のボックス間でフィールドをドラッグしてください:`;
- the Areas `フィルター`, `列`, `行`, `値`, and `合計 / P&L`.

The other words of the reading are in `src/ExPivot.Engine/PivotWords.cs`'s Japanese table at `8436e45e`: `{0} 集計`,
`合計 / {0}`, `データの個数 / {0}`, `(空白)`, `(複数のアイテム)`, `値`, and `全体の {0}`.

**Beside each other:**

- Every word of the reading that ExPivot paints is Excel's.
- `列ラベル` is shown by Excel and not by ExPivot, which draws no corner row (items 12 and 13).
- The pane's two instructions: ExPivot ends each with `:`. Excel's names, read through UI Automation, have no colon.
  No picture of that part of Excel's pane was taken, so whether Excel shows a colon is **not settled by this reading**.

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
5. **Item 27**: Excel paints `列ラベル` over the column Items; ExPivot has no corner row to paint it in (the same as
   items 12 and 13). The Field List's two instructions end in `:` in ExPivot; Excel's, read through UI Automation, do
   not, which a picture would settle (ADR-0060, ADR-0061).
