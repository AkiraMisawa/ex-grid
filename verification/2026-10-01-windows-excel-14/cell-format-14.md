# Windows, fourteenth run, Part A: widths, ja-JP's `mmm`, each cell's own record, Fills and dashes, an edited cell

[`docs/specs/exsheet/verify-on-windows-14.md`](../../docs/specs/exsheet/verify-on-windows-14.md),
Part A, cases 1 to 21. These cases ask Excel about
[ADR-0071](../../docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)'s
"Readings until the fourteenth Windows run". The method is the twelfth run's
([`../2026-10-01-windows-excel-12/cell-format-12.md`](../2026-10-01-windows-excel-12/cell-format-12.md)).

- **Verified commit:** `523c0b944b3ae189cd9bd79cdf38db0add976154`, the tip of
  `claude/exsheet-cell-format` ("Ticket 98: a typed date operand takes its column's date type"). It
  was recorded on the branch `claude/exsheet-windows-verify-14`, cut from that tip.
- **When:** 2026-10-01. Excel ran from 23:30 to 23:52, local time (BST).
- **Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed.
- **The regional format was set back to en-GB** after case 3 (en-US) and after group 3 (ja-JP). Both
  times `HKCU\Control Panel\International` matched the export taken before, line for line, and a new
  PowerShell read `en-GB` (jsonl lines 16 and 20).

**Files.**

| File | Contents |
|---|---|
| `cell-format-14.ps1` | The script: the twelfth run's `cell-format-12.ps1`, copied as the procedure asks, with this run's cases in place of the twelfth's. It takes one case or a list, a pass name, and `-Culture` for the cases under another regional format |
| `cell-format-14.jsonl` | One line per case and pass: the set-up, then for each state the keys sent, what COM read and what the pixels say. Line numbers below are 1-based |
| `shots/` | For each state, A1 to about H16 with the headings (`<case>-<state>.png`); each dialog of Excel's (`…-window-<n>.png`); the crops a case names, enlarged two or four times (`…-x2.png`, `…-x4.png`) |
| `xlsx/` | Group 4: `xl/worksheets/sheet1.xml` and `xl/styles.xml` of each saved workbook. The `.xlsx` files themselves stay on the machine, because their properties name the account |

The whole-screen pictures and the `.xlsx` files are in `%LOCALAPPDATA%\exgrid-layer3\cell-format-14\`.

## Summary

### Where Excel's answer differs from the reading

There are seven: cases 7, 11, 13, 14, 16 and 18, and ADR-0071's reading for case 17.

- **Case 7: Ctrl+5 (strikethrough) widens the column.** The reading was "no change from any of
  them".
  - Ctrl+B and the yellow Fill left column A at the standard width (8.09), showing `########`.
  - **Ctrl+5 widened it to 9.73**, to fit `1234567.50`. `UseStandardWidth` became false.
  - Three passes, with the three in three orders, gave the same answer (passes b, c and d):
    - b: Ctrl+B, then Ctrl+5, then the Fill.
    - c: Ctrl+5 first.
    - d: the Fill first.
  - **Pass a could not ask the question.** In it, the Number Format `0.00` was set through COM on A1
    while A1 already held the value. **That set-up alone widened column A to 9.73**, before any key.
    Passes b to d set the format on the empty A1 first, then the value; then A1 showed `########` at
    8.09, as the procedure means.
- **Case 11: the dialog's `d-mmm-yy` is not built-in 15.** The reading was "the same as 10".
  - Four of the five read as in case 10. **A2 differs.**
  - In case 10, A2's `d-mmm-yy` was set through COM in en-US codes. That is built-in 15, whose local
    code under ja-JP is `dd-mmm-yy`, and it showed `05-1-26`.
  - In case 11, `d-mmm-yy` typed into Format Cells kept the local code `d-mmm-yy` and showed
    **`5-1-26`**.
  - The other way round, `dd-mmm-yy` typed into the dialog reads back as built-in 15 reads:
    `NumberFormat` `d-mmm-yy`, the local code `dd-mmm-yy`.
  - Every `mmm` showed the month as a number in both cases (group 3, below).
- **Case 13: Format Cells shows A2 with a top line.** The reading was "no top: the dialog shows A2's
  own sides".
  - With A2 alone selected, the Border tab's preview draws a **thick line along its top**, and the
    top-border button is pressed (`13-tab-border-window-1.png`).
  - A2 records nothing of its own. In the saved file A2 has no `c` element, so its style is 0, a
    border with no sides. A1 records its thick bottom (`s="1"`).
  - Added: over A1 the same tab draws the thick line along the bottom (`13-a1-opened-window-1.png`).
- **Case 14: Inside over rows 3:4 also sets A's left.** The reading was "the line between rows 3 and
  4 and between columns; XFD's right set; A's left not set".
  - **A3's left and A4's left read `Continuous Thin #000000`** through COM. `Rows(3)` and `Rows(4)`
    read a left edge as well.
  - In the file, rows 3 and 4 are formatted as whole rows (`customFormat="1"`). Row 3's border has
    left, right and bottom; row 4's has left, right and top. So A3 and A4 record a left.
  - The rest is as read:
    - the line between rows 3 and 4 is drawn black;
    - the lines between columns are drawn;
    - XFD3's and XFD4's right edges are set, and drawn black at the Sheet's right edge.
  - A's left lies on the Row Headings' edge (`#ABABAB`), so the picture cannot show it.
- **Case 16: the gridline between two filled cells takes the lower cell's Fill.** The reading was
  "yellow (the upper cell's)".
  - The gridline between B2 (yellow) and B3 (light blue) is drawn **light blue (`#00B0F0`)**.
  - Between B2 and C2 (unfilled) it is yellow, as read.
- **Case 17 (ADR-0071's reading): the middle pixel of a double line is the Fill.** ADR-0071 reads
  "a double line's middle pixel is the grid's ground, even over a Fill". The procedure's table asks
  only "record whether that middle pixel is white or yellow".
  - Excel draws dark, then the gridline's pixel, then dark. **The middle pixel is yellow
    (`#FFFF00`)**, B2's Fill.
- **Case 18: the long dash is 9 pixels at every zoom.** The reading was "8 on below 150%, 9 from
  150%".
  - Medium dashed reads **9 on, 3 off** at zoom 100%, 125%, 150%, 175% and 200%.
  - Added: at 50%, 60%, 75% and 90% it also reads 9 on, 3 off. On this display at 150%, those zooms
    draw at 0.75 to 1.35 device pixels per pixel of zoom 100% at 96 DPI.
  - Thin dashed reads 3 on, 1 off at every zoom.
  - Only the first run in each row is shorter. The reading starts 2 px inside the cell, so it cuts
    the first dash.

Every other case with a reading matched it: 1, 2, 4, 5, 6, 8, 9, 12, 15, 20 and 21. Case 19 is
marked "record" and matched what its reading names. The details are in the tables below.

### Cases marked "record", and what was recorded

- **Case 1: a column already widened by the date key widens again.**
  - Ctrl+# on A1 widened column A from 8.09 to 8.73 (`09-Dec-25`).
  - Then Ctrl+Shift+$ on A2 widened it again, to **11.82**, to fit `£1,234,567.50`.
  - This is the reading; the eleventh run's hint did not hold here.
- **Case 3: the eleventh run's case 20 under en-US, with the width after each key.** No `########`
  appeared. A1 = 1234.5, column A at 8.09:

  | Key (UK layout, as the character) | `NumberFormat` | Text | Width | `UseStandardWidth` |
  |---|---|---|---|---|
  | Ctrl+Shift+`~` | `General` | `1234.5` | 8.09 | true |
  | Ctrl+Shift+`!` | `#,##0.00` | `1,234.50` | 8.09 | true |
  | Ctrl+Shift+`@` | `h:mm AM/PM` | `12:00 PM` | 8.09 | true |
  | Ctrl+`#` | `d-mmm-yy` | `18-May-03` | **8.73** | false |
  | Ctrl+Shift+`$` | `$#,##0.00_);[Red]($#,##0.00)` | `$1,234.50 ` | **8.91** | false |
  | Ctrl+Shift+`%` | `0%` | `123450%` | 8.91 | false |
  | Ctrl+Shift+`^` | `0.00E+00` | `1.23E+03` | 8.91 | false |

  - The date key widened column A to 8.73 first, as ADR-0071 supposes.
  - **Then `$` widened it again, to 8.91**, and `$1,234.50 ` showed in full. So the `########` of the
    eleventh run was not reproduced.
  - Excel's `International` under en-US: country setting 1, `$`, date order 0.
- **Case 6: a bold number that fits the standard width still fits bold.**
  - `12345678` in bold fitted column A at 8.09, about 4 px from its left edge (`6-parked-x4.png`).
  - The width did not change.
- **Case 10: under ja-JP, `mmm` shows the month as a number in every code; `mmmm` shows `1月`.**

  | Cell | Code asked (COM, en-US codes) | `NumberFormat` | `NumberFormatLocal` | Text |
  |---|---|---|---|---|
  | A1 | `dd-mmm-yy` | `dd-mmm-yy` | `dd-mmm-yy` | `05-1-26` |
  | A2 | `d-mmm-yy` | `d-mmm-yy` | `dd-mmm-yy` | `05-1-26` |
  | A3 | `mmm d, yyyy` | `mmm d, yyyy` | `mmm d, yyyy` | `1 5, 2026` |
  | A4 | `mmmm` | `mmmm` | `mmmm` | `1月` |
  | A5 | `yyyy/mmm/dd` | `yyyy/mmm/dd` | `yyyy/mmm/dd` | `2026/1/05` |

  - Excel's `International` under ja-JP: country setting 81, `¥`, date order 2. General's local code
    reads `G/標準`.
- **Case 15: Inside over the whole Sheet sets every side, the top of row 1 and the bottom of row
  1048576 included.**
  - A1's left and top, B1's top, XFD1's right, B2's top, A1048576's bottom and XFD1048576's right
    and bottom all read `Continuous Thin #000000`.
  - **The top of row 1 is set.** It lies on the Column Headings' edge (`#ABABAB`), so the picture
    cannot show it.
  - **The bottom of row 1048576 is set**, and drawn black.
  - In the file, one `col` element covers every column (`min="1" max="16384"`, `style="1"`). Its
    border has all four sides thin, and no row or cell is written.
- **Case 18: the patterns** are in group 5's table.
- **Case 19: the edited cell keeps its Fill and Font. The Selection's outline covers its lines.**
  - While editing: yellow ground, red text, bold, italic and underlined (`19-editing-x4.png`).
  - **The bottom and right lines do not show while editing.** They do not show with B2 selected
    either: the Selection's outline lies over them in both states.
  - When the edit opens, the outline's inner white ring goes. The yellow then reaches the outline.
- **Case 21: `0;[Red]@`.** `5`, `-5` and `0` show black, and `abc` shows red (`#FF0000`). This is
  the reading. COM reads each cell's Font colour as `#000000`.

## Environment

| | |
|---|---|
| Excel | Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), x64; `Application.Build` 20430. English (UK) interface |
| Office Theme | "Use system setting" (File › Account, read through UI Automation) |
| Windows | Light mode (apps and system); high contrast off; display scale **150%** (144 DPI); work area 3840 × 2088 |
| Regional format | en-GB (`dd/MM/yyyy`, `HH:mm:ss`, `£`). Case 3 ran under en-US, and cases 10 and 11 under ja-JP (`Set-Culture`, before a new Excel), each set back to en-GB |
| Excel's standard font | Aptos Narrow, 11. The standard column width is 8.09 |
| Keyboard | Excel's window was on English (UK), HKL `0x08090809`, with the IME closed, in every case, and was put back to it at the end of every run |
| Languages | en-GB (0809), en-US (0409), ja (Microsoft IME); the Japanese layout runs over `kbd101.dll`. No case used it |
| Zoom | 100%, except case 18 |
| Gridlines | Excel draws them `#E0E0E0` (sampled) |

Case 0 (jsonl line 1) typed every character the cases send into A1 (`'~!@#$%^&_=1234.5abc`), and A1
read it back exactly.

## Method

The twelfth run's method, unchanged. In brief:

- **Excel.** Each case ran in an Excel of the script's own, started with `New-Object` and checked to
  be a new process.
  - It was ended at the end of the case, by `Quit`, then by `Stop-Process`: none had quit within
    10 s.
  - No running Excel was attached to. The user's AutoRecovered and unsaved workbooks were not
    touched: they hash as the copy taken before the run.
  - Each case got a fresh workbook with one sheet, `Sheet1`, maximised at 100%, with A1 in view and
    selected.
- **Input.**
  - Keys went through `SendInput` (a virtual-key and the layout's scan code), including every move
    to a cell a case names.
  - The mouse did what a case names: Format Cells' tabs, categories, presets, checkbox and buttons
    (cases 8, 9, 11, 13 to 15), the Fill Colour button (case 7) and the corner box (case 15).
- **COM** only set cases up, saved the workbook (group 4), set the zoom (case 18), and read results.
- **Pictures** were taken with `PrintWindow` (`PW_RENDERFULLCONTENT`), each window of Excel's laid at
  its place.

**What this run added to the script.**

- **The width after every key** (groups 1 and 2). After each key: column A's `ColumnWidth`, its width
  in points and `UseStandardWidth`, the Sheet's `StandardWidth`, and each named cell's `Text`,
  `NumberFormat`, `NumberFormatLocal`, bold, strikethrough and Fill. If a key had opened an edit or a
  dialog, the state would have been read from the picture, then Esc pressed. None did.
- **`-Culture`.** The script sets the regional format with `Set-Culture` before the first Excel
  starts, and sets back the one it began with at the end. It exports `HKCU\Control Panel\International`
  before and after and compares the two. A case that needs a culture refuses to run under another.
- **The Fill Colour button** (case 7) was found through UI Automation (`SplitButton` "Fill Colour"),
  as the eleventh run found Font Colour. The click went on its face, left of the arrow. In a new
  Excel the face fills yellow: COM read `#FFFF00` afterwards.
- **Each cell's own record** (group 4). The workbook was saved through COM (`SaveAs`, `.xlsx`) and
  the file read on the machine. Excel keeps a saved workbook's file open, so the file was opened with
  every sharing allowed.
  - Each named cell's style index is taken from the first of these that exists:
    - its own `c` element;
    - its row, if the row has `customFormat`;
    - the `col` element that covers it;
    - otherwise 0.
  - Recorded with it: the `cellXfs` entry at that index, and the `border` that entry points to.
- **A dash's runs** (case 18). Each pixel row near the edge is read along the cell's width, less
  2 px at each end. On is luminance below 128.

**Additions to the procedure.** Each is marked in the record.

- **Case 5, pass b:** `123456789` typed into A1 of a new workbook, for the width typing gives. It is
  **9.18**, the width Ctrl+Shift+`~` gave.
- **Case 7, passes b to d** (above).
- **Case 13:** the same dialog over A1, and the workbook saved.
- **Case 15:** the workbook saved.
- **Case 18:** zoom 150%, and pass b at 50%, 60%, 75% and 90%.

**One step failed, and was run again.** The first run of cases 12 to 15 (jsonl lines 21 to 24)
failed at the read of the saved file. Excel held it open, and the script opened it without sharing.
The read was changed, and the four cases ran again from the start (lines 25 to 28). Their pictures
are the second run's.

## Group 1: a key on a column that has already widened

Widths are `ColumnWidth`; "std" is `UseStandardWidth`.

| # | Set up | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 1 | A1 = 46000.5, A2 = 1234567.5; A at 8.09 | Ctrl+# on A1; Down; Ctrl+Shift+$ on A2 | A widens again to fit `£1,234,567.50` (record) | **As read.** After `#`: 8.73, std false, A1 `09-Dec-25`. After `$`: **11.82**, A2 `£1,234,567.50` | `1-set-x2.png`, `1-hash-x2.png`, `1-dollar-x2.png` |
| 2 | `123456789012` typed into A1 with Enter; then A2 = 1234567.5 (COM) | Ctrl+Shift+$ on A2 | A widens again (SH-26) | **As read.** Typing widened A to 11.18 (A1 `1.23457E+11`). After `$`: **11.82**, A2 `£1,234,567.50`. The active cell after Enter was A2 | `2-typed-x2.png`, `2-dollar-x2.png` |
| 3 | en-US; A1 = 1234.5 | the seven keys in turn | record | The table above: the date key 8.73, then `$` **8.91**; no `########` | `3-*-x2.png` |
| 4 | A at 12 (COM); A1 = 1234567890.5 | Ctrl+Shift+$ | A stays 12, `########` | **As read.** 12, `############` | `4-set-x2.png`, `4-dollar-x2.png` |

## Group 2: what else widens

| # | Set up | Keys / mouse | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 5 | A1 = 123456789 in `0.00E+00` (`1.23E+08`) | Ctrl+Shift+~ | widens as typing would | **As read.** 8.09 → **9.18**, `123456789`, General. Pass b: typing `123456789` into a new A1 gave 9.18 too | `5-tilde-x2.png`, `5-b-typed-x2.png` |
| 6 | A1 = 12345678 | Ctrl+B | a Font never widens; record whether it fits bold | **No widening; it fits bold.** 8.09, std true, `12345678` in bold | `6-bold-x2.png`, `6-parked-x4.png` |
| 7 | A1 = 1234567.5 in `0.00` (`########` at 8.09) | Ctrl+B, Ctrl+5, the Fill Colour button (mouse) | no change from any | **Ctrl+5 widens: 8.09 → 9.73**, `1234567.50`. Ctrl+B and the Fill: no change. Passes b, c, d (orders above). Pass a's COM set-up had already widened A to 9.73 | `7-b-*-x2.png`, `7-c-*-x2.png`, `7-d-*-x2.png` |
| 8 | A1 = 1234567.5 (General, `1234568`) | Ctrl+1; Number tab; category Number (2 decimal places, as it opened); Use 1000 Separator ticked; OK | widens to fit `1,234,567.50` | **As read.** 8.09 → **10.82**, `1,234,567.50`, `#,##0.00` | `8-*-window-1.png`, `8-ok-x2.png` |
| 9 | As 8 | Ctrl+1; Border tab; Outline; OK | no change | **As read.** 8.09, std true. A1's four edges read `Continuous Thin #000000` | `9-ok-x2.png` |

## Group 3: `mmm` under ja-JP

| # | Set up | How the codes were set | Reading | Excel |
|---|---|---|---|---|
| 10 | ja-JP; column A at 20; A1:A5 = 5 January 2026 | COM `NumberFormat`, en-US codes | built-in 15 shows `05-1-26`; record the others | **The month is a number in every `mmm`; `mmmm` is `1月`.** The table above (`10-set-x2.png`) |
| 11 | As 10, General | Format Cells, Custom: each code typed into the Type box, OK | the same as 10 | **The same, but A2 shows `5-1-26`.** Typed `dd-mmm-yy` reads back as built-in 15 reads (`NumberFormat` `d-mmm-yy`, local `dd-mmm-yy`, `05-1-26`). Typed `d-mmm-yy` stayed a code of its own (local `d-mmm-yy`, **`5-1-26`**). A3 `1 5, 2026`, A4 `1月`, A5 `2026/1/05`, as in 10 (`11-a5-ok-x2.png`; each dialog `11-a<n>-typed-window-1.png`) |

## Group 4: what each cell records, read from the saved file

The file's border is written as its sides that hold a line. `thin`/`thick` and the colour are as the
file has them (`auto` is Automatic).

| # | Set up / keys | Reading | COM and the picture | The file | Pictures |
|---|---|---|---|---|---|
| 12 | C2's left thin blue; E5's right thick red, E5 = 1. E5, Ctrl+C, B2, Ctrl+V | B2 records a thick red right; C2 keeps its thin blue left; the thick red line is drawn | B2's right and C2's left both read `Continuous Thick #FF0000`; drawn red (`-3..-1 ff0000`) | **As read.** B2 `s="2"`: right thick `FFFF0000`. C2 `s="1"`: left thin `FF0000FF`. E5 `s="2"`. F5 records nothing | `12-set-x4.png`, `12-parked-x4.png` |
| 13 | A1's bottom thick black. A2 alone, Ctrl+1, Border tab; Cancel | no top on A2's preview | A1's bottom and A2's top read `Continuous Thick #000000` | A1 `s="1"`: bottom thick. A2: no `c` element, style 0, no sides | **The preview shows a thick top**, and the top button is pressed: `13-tab-border-window-1.png`. Over A1 (added): a thick bottom, `13-a1-opened-window-1.png` |
| 14 | Rows 3:4 (A3, Shift+Space, Shift+Down); Ctrl+1, Border, Inside, OK | the lines between rows 3 and 4 and between columns; XFD's right set; A's left not set | **A3's and A4's left read `Continuous Thin`**; the line between rows 3 and 4, the lines between columns, and XFD3's and XFD4's right read and are drawn black | Rows 3 and 4 written as whole rows: row 3 `s="1"` (left, right, bottom), row 4 `s="2"` (left, right, top). Every cell of the rows takes its row's | `14-parked-x4.png`, `14-right-x4.png` |
| 15 | The corner box; Ctrl+1, Border, Inside, OK | every side, A's left and XFD's right included; record row 1's top and row 1048576's bottom | **As read.** Every edge read is `Continuous Thin #000000`, row 1's top and row 1048576's bottom included. Drawn black wherever a picture can show it | One `col` element, `min="1" max="16384"`, `style="1"`: all four sides | `15-parked-x4.png`, `15-bottom-x4.png`, `15-right-x4.png`, `15-bottom-right-x4.png` |

## Group 5: Fills and lines where two cells meet, and the dashes

**Where the gridlines are.** In column C, which is unfilled, the pixel rows of the gridlines were
read straight from the pictures: y = 381, 410 and 439. These lie between rows 1/2, 2/3 and 3/4, at
29 px a row (14.5 pt at 150%).

`PointsToScreenPixelsY` gives 381, 411 and 441 for the same boundaries, drifting by a pixel a row. So
the offsets in the jsonl's `across` reads count from a coordinate that is not the gridline, and the
gridline's own pixel is named here by its y.

| # | Set up | Reading | Excel | Pictures |
|---|---|---|---|---|
| 16 | B2 yellow, B3 `#00B0F0`; no borders; the Selection at G14 | B2/B3: yellow (the upper cell's); B2/C2: yellow | **B2/B3 (y 410): light blue, the lower cell's.** B2/C2 (x 231): yellow, as read. Also: the gridline above B2 (y 381) is yellow, the one left of B2 (x 135) yellow, the one below B3 (y 439) light blue. Column B runs yellow 381–409, then light blue 410–439 | `16-set-x4.png` |
| 17 | B2 yellow, double bottom black (Double, Thick); row 2 became 15 pt | dark, the gridline's pixel, dark; record the middle | **Dark (y 410), yellow (y 411, the gridline in column C), dark (y 412)** | `17-set-x4.png` |
| 18 | B2 medium dashed bottom, B4 dashed (thin) bottom | 8 on below 150%, 9 from 150%; record each zoom | **9 on, 3 off at every zoom**, as below | `18-zoom-<z>-x4.png`, `18-b-zoom-<z>-x4.png` |

Case 18's runs, along each cell's bottom less 2 px at each end (`+n` on, `-n` off). The first run is
cut by where the reading starts.

| Zoom | B2, medium dashed (each dark pixel row reads the same) | B4, dashed |
|---|---|---|
| 50% (added) | `+4 -3 +9 -3 +9 -3 +9 -3 +1` | `+1 -1 +3 -1 +3 …` |
| 60% (added) | `+7 -3 +9 -3 +9 -3 +9 -3 +9` | `+3 -1 +3 -1 …` |
| 75% (added) | `+8 -3 +9 -3 +9 -3 +9 -3 +9 -3 +9` | `+3 -1 +3 -1 …` |
| 90% (added) | `+4 -3 +9 -3 +9 …` | `-1 +3 -1 +3 …` |
| 100% | `+2 -3 +9 -3 +9 -3 +9 -3 +9 …` | `+3 -1 +3 -1 …` |
| 125% | `+4 -3 +9 -3 +9 …` | `-1 +3 -1 +3 …` |
| 150% (added) | `+5 -3 +9 -3 +9 …` | `-1 +3 -1 +3 …` |
| 175% | `+8 -3 +9 -3 +9 …` | `+2 -1 +3 -1 +3 …` |
| 200% | `-2 +9 -3 +9 -3 +9 …` | `+3 -1 +3 -1 …` |

Every full dash of B2 is 9 on and 3 off, and every one of B4 is 3 on and 1 off. The pattern in
device pixels does not change with the zoom. Row 2 read 15 pt at every zoom of the first pass.

## Group 6: a formatted cell while it is edited, and `0;[Red]@`

Colours are sampled from the pictures (the most frequent pixel unlike the ground).

| # | Set up | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 19 | B2 = 12345: bold, italic, single underline, red; Fill yellow; thick black bottom and right | Ctrl+Home, Down, Right; F2; Esc | yellow ground, red bold italic underlined text; the lines hidden under the edit (record) | **Editing: ground `#FFFF00`, text `#FF0000`, bold, italic, underlined.** Parked, the thick lines show (`-3..-1 000000`). Selected and editing, the Selection's outline (`#217346`) lies over them. Editing removes the outline's inner white ring. A caret stands after `5` | `19-set-x4.png`, `19-selected-x4.png`, `19-editing-x4.png` |
| 20 | B2 = -5 in `0;[Red]-0`, Font blue | as 19 | blue while edited, not red | **As read.** Parked and selected: red (`#FF0000`). Editing: **blue (`#0000FF`)**. After Esc: red | `20-set-x4.png`, `20-editing-x4.png` |
| 21 | A1:A4 = 5, -5, 0, `abc`, each `0;[Red]@` | — | `5` black, `-5` and `0` black, `abc` red (record) | **As read.** `5`, `-5`, `0`: `#000000`; `abc`: `#FF0000` | `21-set-x4.png` |

## Seen beside the readings

These answers were not asked for.

- **A Number Format set through COM widens a standard-width column** whose cell already holds the
  value: case 7's pass a, `0.00` on 1234567.5, gave 9.73. ADR-0071 notes the same of two corpus cases
  (FMT-052, FMT-072).
- **The eleventh run's 100% reading of medium dashed already holds a 9 px dash.** Its pattern
  `########...#########...` starts 8 px inside the cell, so its first run is cut. The second run is
  9 on.
- **`PointsToScreenPixelsY` drifts from the gridlines** by a pixel a row at this scale (group 5). The
  twelfth run's offsets counted from the same coordinate, near the top of the Sheet.
- **A saved Inside over the whole Sheet writes no rows and no cells**, only one `col` element (case
  15). It also writes that element's width, `8.7265625`.
- **No Excel quit within 10 s of `Quit`.** All 31 were the script's own, and each was ended by
  `Stop-Process`. None was left running at the end.

## The machine afterwards

- No Excel is running.
- The AutoRecover folder holds the same three files, each hashing as the copy taken before the first
  Excel started (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run14`).
- The regional format is en-GB, and the keyboard English (UK).
