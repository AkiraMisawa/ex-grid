# What to verify on Windows, thirteenth run

Status: ready-for-human. Part A asks Excel only and needs no build. (Part C of the
[eleventh run](verify-on-windows-11.md) still waits for tickets 48 and 49.)

This is for the Claude Code session on the Windows desktop of the earlier runs.
- Read [`verify-on-windows-11.md`](verify-on-windows-11.md) and
  [`verify-on-windows-12.md`](verify-on-windows-12.md) first. Their Setup, method and tools apply
  here unchanged.
- **Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
  `docs/definition-of-done.md`.

Part A settles the readings in
[ADR-0063](../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Readings
until the thirteenth Windows run". Ticket 58 built ExSheet on those readings, so each answer either
confirms the code or names what to change. The readings cover three things:
- when a formatting key widens a column that is no longer at the standard width;
- what else widens a column;
- how ja-JP shows `mmm`;
- what each cell records for itself under the edge model of ticket 57.

## Setup

- **Branches.** Fetch **`claude/exsheet-cell-format`**. Branch **`claude/exsheet-windows-verify-13`**
  from its tip, and record that tip as the verified commit.
- **Authorisation.** The user has authorised this run in advance: real keys and mouse to Excel.
  **Do not stop to ask.** Say "starting" before the first input and "finished" after the last.
- **Tools.** Copy the twelfth run's `cell-format-12.ps1` as
  `verification/<date>-windows-excel-13/cell-format-13.ps1`, taking one case or a list of cases.
- **Keys and COM.** Every operation a case asks about is done with real keys, or with the mouse where
  the case says so. COM only sets up a case and reads the result.
- **Widths.** Read `ColumnWidth` and `UseStandardWidth` **after every key**, not only at the end, and
  read the text shown (`Text`) as well.
- **Environment.** Record it as before. Use the regional format en-GB unless a case says otherwise.
  The standard width is the one the earlier runs recorded (8.09), unless a case says otherwise.

## Part A — Excel

Use a fresh workbook for each case, with one sheet named `Sheet1`, at 100% zoom. Record each case as
one JSON line in `cell-format-13.jsonl` and as a row in `cell-format-13.md`.

### Group 1 — a key on a column that has already widened

| # | Set up (COM) | Keys | What is asked | Reading |
|---|---|---|---|---|
| 1 | Column A at the standard width. A1 = 46000.5, A2 = 1234567.5 | Ctrl+`#` on A1 (the date key; it widened column A to 8.73 in run 12's case 17). Then Ctrl+Shift+`$` on A2 | Column A's width and `UseStandardWidth` after each key; A2's text | A widens again to fit `£1,234,567.50`. The eleventh run's case 20 hints that it does not: record |
| 2 | Column A at the standard width. **Type** `123456789012` into A1 with real keys and Enter (an entry that widens the column). Then A2 = 1234567.5 (COM) | Ctrl+Shift+`$` on A2 | The same | A widens again (SH-26: a width widened by an entry is widened again) |
| 3 | Under **en-US** (`Set-Culture`, a new Excel, as in the eleventh run's case 20): A1 = 1234.5, column A at the standard width | On A1, in this order: Ctrl+Shift+`~`, Ctrl+Shift+`!`, Ctrl+Shift+`@`, the date key (Ctrl+`#`, with no Shift on the UK layout), Ctrl+Shift+`$`, Ctrl+Shift+`%`, Ctrl+Shift+`^` | After each key: `NumberFormat`, the text, the width, `UseStandardWidth` | This repeats the eleventh run's case 20 under en-US with the width read after each key. It settles why `$` showed `########` there |
| 4 | Column A set to width 12 by hand (COM `ColumnWidth = 12`), then A1 = 1234567890.5 | Ctrl+Shift+`$` on A1 | The width and the text | A stays 12 and shows `########` (run 12's case 18) |

Restore en-GB after case 3, and say so.

### Group 2 — what else widens

| # | Set up (COM) | Keys / mouse | What is asked | Reading |
|---|---|---|---|---|
| 5 | A1 = 123456789 in `0.00E+00` (shows `1.23E+08`), column A at the standard width | Ctrl+Shift+`~` (General) on A1 | The width, the text | General widens as typing `123456789` would. Record what Excel shows if it does not widen |
| 6 | A1 = 12345678 (General, fits at the standard width), column A at the standard width | Ctrl+B on A1 | The width, the text | A bold number that no longer fits shows `####`. A Font never widens. Record whether it fits bold at all |
| 7 | As 6, with A1 = 1234567.5 in `0.00` (shows `1234567.50`, which does not fit) | Ctrl+B, then Ctrl+5 (strikethrough), then a yellow Fill from the ribbon (mouse) | The width after each | no change from any of them |
| 8 | A1 = 1234567.5 (General), column A at the standard width | With the mouse: Ctrl+1 → Number → Number, 2 decimal places, *Use 1000 Separator* ticked → OK | The width, the text | widens to fit `1,234,567.50`, as the key does |
| 9 | As 8 | With the mouse: Ctrl+1 → Border → Outline → OK | The width | no change |

### Group 3 — `mmm` under ja-JP

| # | Set up (COM) | What is asked | Reading |
|---|---|---|---|
| 10 | Under **ja-JP** (`Set-Culture`, a new Excel): A1:A5 = the date 5 January 2026, formatted `dd-mmm-yy`, `d-mmm-yy`, `mmm d, yyyy`, `mmmm`, `yyyy/mmm/dd`. Widen column A to 20 first | The text of each, and `NumberFormatLocal` | Built-in 15 shows `05-1-26` (run 12's case 19). Whether the others show the month as a number, as `1月`, or otherwise: record |
| 11 | As 10, but type each code into Format Cells → Custom (mouse and keys) instead of COM | The same | the same as 10 |

Restore en-GB after group 3, and say so.

### Group 4 — what each cell records, where the runs saw only the edge

Ticket 57 built Excel's edge model (ADR-0063, "What the twelfth Windows run settled"). These cases read
what the model says each cell keeps for itself. **Save as `.xlsx` and read the file**: unzip it, then
record each named cell's `s` index in `xl/worksheets/sheet1.xml`, its `cellXfs` entry and the `border`
it points to in `xl/styles.xml`.

| # | Set up (COM) | Keys / mouse | What is asked | Reading |
|---|---|---|---|---|
| 12 | As the twelfth run's case 1: C2's left thin blue; E5's right thick red, E5 = 1 | Select E5, Ctrl+C, select B2, Ctrl+V. Save | B2's and C2's own `border` in the file; the line drawn | B2 records a thick red right; C2 still records its thin blue left; the thick red line is drawn |
| 13 | A1's bottom thick black; A2 plain | Select A2 alone, Ctrl+1, the Border tab | A screenshot of the preview: does it show a top line? Then Cancel | no top: the dialog shows A2's own sides |
| 14 | — | Select rows 3:4 (A3, Shift+Space, Shift+Down), Ctrl+1 → Border → **Inside** (mouse) → OK. Save | A3's and A4's left edges, XFD3's right edge, B3's bottom edge (COM and the file) | the line between rows 3 and 4 and between columns; XFD's right set; A's left not set |
| 15 | — | Select the whole Sheet (the corner box, mouse), Ctrl+1 → Border → **Inside** → OK | A1's left and top, XFD1's right, B2's top, A1048576's bottom (COM) | every side, including A's left and XFD's right. Record the top of row 1 and the bottom of row 1048576 |

## Results

- Push `claude/exsheet-windows-verify-13` with `verification/<date>-windows-excel-13/`.
- In `cell-format-13.md`, summarise two lists:
  - every case whose answer differs from its reading;
  - every case marked "record".
- Restore the regional format afterwards, and say so.
