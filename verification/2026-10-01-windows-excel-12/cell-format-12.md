# Windows, twelfth run, Part A: the line between two cells, insertion and deletion, and the keys

[`docs/specs/exsheet/verify-on-windows-12.md`](../../docs/specs/exsheet/verify-on-windows-12.md),
Part A, cases 1 to 19. These cases ask Excel about
[ADR-0063](../../docs/adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)'s
"Readings until the twelfth Windows run". The method is the eleventh run's
([`../2026-10-01-windows-excel-11/cell-format.md`](../2026-10-01-windows-excel-11/cell-format.md)).

- **Verified commit:** `a0ed1e60dbb00b52e0bc9854fd5d07eb075eb81d`. It is the tip of
  `claude/exsheet-cell-format` ("The twelfth Windows run: the line between two cells after other
  operations, and what insertion and deletion leave"). It was recorded on the branch
  `claude/exsheet-windows-verify-12`, which was cut from that tip.
- **When:** 2026-10-01. Excel ran from 12:25 to 12:40, local time.
- **Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed.

**Files.**

| File | Contents |
|---|---|
| `cell-format-12.ps1` | The script. It is the eleventh run's `cell-format.ps1`, copied as the procedure asks, with the twelfth run's cases in place of the eleventh's. It takes one case or a list of cases, and a pass name |
| `cell-format-12.jsonl` | One line for each case and pass: the set-up, then for each state the keys sent, what COM read and what the pixels say. The jsonl line numbers given below are 1-based |
| `shots/` | For each state, a picture of A1 to about H16 with the headings (`<case>-<state>.png`). There is a cut of each dialog of Excel's (`…-window-<n>.png`), and the crops a case names, enlarged two or four times (`…-x2.png`, `…-x4.png`) |

The whole-screen pictures stay on the machine. They are in `%LOCALAPPDATA%\exgrid-layer3\cell-format-12\`.

## Summary

### Where Excel's answer differs from the reading

There are three.

- **Case 10, a row inserted at row 1.** The reading was "the line goes; the new row 1 takes
  nothing".
  - **The line does not go.** It stays on the old B1, which is now B2: B2's top edge reads
    `Continuous Thick #000000`.
  - The new B1's bottom edge reads the same line, because it is the edge the two cells share. The
    picture draws it between rows 1 and 2.
  - The new B1 takes nothing of its own: its top, left and right edges read `None`, and every one
    of `Rows(1).Borders` reads `None`.
- **Case 14, an outline over row 3 (Ctrl+Shift+&).** The reading was "top and bottom only".
  - Excel also recorded **A3's left edge**, the left edge of the Sheet. It reads `Continuous Thin
    #000000`, and `Rows(3).Borders(xlEdgeLeft)` reads the same.
  - The right end was not recorded: XFD3's right edge and `Rows(3).Borders(xlEdgeRight)` read
    `None`.
  - Top and bottom were recorded as the reading says. `Rows(3)` top and bottom read `Continuous
    Thin`, and so do the top and bottom of every cell of row 3 that was read (A3 to C3, XFC3 and
    XFD3).
  - The picture shows no line at A3's left. The pixel there is the heading's edge (`#ABABAB`), as
    in every other row.
- **Case 15, an outline over the whole Sheet.** The reading was "the left of A and the right of XFD
  only".
  - **Excel recorded nothing.** Every edge asked about reads `None`: A1's top and left, XFD1's
    right, A1048576's bottom, `Columns(1).Borders(xlEdgeLeft)`, `Columns(16384).Borders(xlEdgeRight)`
    and `Rows(1).Borders(xlEdgeTop)`. So did `Cells.Borders`, every edge of `Rows(1048576)`, and
    A1:B2.
  - No dialog or message showed, and the selection stayed `1:1048576`.
  - Three passes gave the same answer:
    - **Pass a:** the whole Sheet selected by Ctrl+A twice.
    - **Pass b (added):** the whole Sheet selected by a click on the corner box, and a 2 s wait
      after the key before the reads.
    - **Pass c (added):** pass a again, with the same 2 s wait.
  - The same key on row 3 (case 14), in the same way, did record an outline.

Every other case with a reading matched it: 1, 2, 3, 4, 5, 8, 11, 12, 13 and 18. The details are
in the tables below.

### Cases marked "record", and what was recorded

- **Case 6, two rows meeting when row 3 is deleted.**
  - Before the delete, B2's bottom edge was thick black and B4's top edge was thin blue.
  - Afterwards, **the upper row's line remains**:
    - B2's bottom edge and the new B3's top edge (the old B4) both read
      `Continuous Thick #000000`.
    - The picture draws the thick black line there.
    - The thin blue line is gone, from COM and from the picture.
  - This is the line ADR-0063 reads as winning until this answer.
- **Case 7, the same with columns: column C deleted.**
  - Before the delete, B2's right edge was thick black and D2's left edge was thin blue.
  - Afterwards, **the left column's line remains**. B2's right edge and the new C2's left edge
    both read `Continuous Thick #000000`, and the thick black line is drawn.
  - The thin blue line is gone.
- **Case 9, row 2 deleted, with the line set from B3.**
  - Before the delete, B3's top edge was thin blue, set from B3, so B2's bottom read it too.
  - Afterwards, **the line remains**. B1's bottom edge and the new B2's top edge (the old B3) both
    read `Continuous Thin #0000FF`, and the picture draws it blue on the gridline's pixel.
  - B1 had no line of its own before the delete.
- **Case 16, Inside over columns B:C (Format Cells, the Border tab).** The inside line also shows on
  the top of row 1 and on the bottom of row 1048576, as ticket 55 reads it.
  - **Row 1:** B1's top and C1's top read `Continuous Thin #000000`.
  - **Row 1048576:** B1048576's bottom and C1048576's bottom read the same.
  - **The levels:**
    - `Range("B:C").Borders` reads top, bottom, inside vertical and inside horizontal
      `Continuous Thin`; left and right read `None`.
    - `Columns(2)` and `Columns(3)` each read top, bottom and inside horizontal `Continuous`, as
      well as the side facing the other column.
  - **Drawn:**
    - At row 1048576, the line is drawn black on its bottom edge.
    - At row 1, B1's top edge falls on the heading's edge (`#ABABAB`). The picture cannot show
      whether a line lies under it.
    - The horizontal lines stop at the left of B and the right of C, and nothing is drawn outside
      B:C.
- **Case 17, which keys widen a standard-width column.**
  - **Pass a, the procedure's values.** The date key, the `$` key and the `!` key widened their
    columns:
    - Ctrl+# on A1: from 8.09 to 8.73 (`09-Dec-25`).
    - Ctrl+Shift+$ on B1: to 11.82 (`£1,234,567.50`).
    - Ctrl+Shift+! on C1: to 10.82 (`1,234,567.50`).
  - The other three texts fit the standard width, so pass a cannot say whether those keys widen:
    D1 showed `12%`, E1 `1.23E+06` and F1 `00:00`, and their columns stayed 8.09.
  - **Pass b (added).** The Sheet's standard width was set to 4 (COM `StandardWidth`), with every
    column still at the standard width, and D1 = 1234567.5, F1 = 46000.5. No text fitted, and
    **every key widened its column**:
    - The date key: 8.73. `$`: 11.82. `!`: 10.82.
    - `%`: 10.73 (`123456750%`). `^`: 7.73 (`1.23E+06`). `@`: 4.73 (`12:00`).
  - In both passes, a widened column no longer reads `UseStandardWidth`.
- **Case 19, built-ins 15 and 20 under three regional formats.** A1 held the date 5 January 2026 and
  A2 the time 09:05, both General.
  - **Excel localises both keys.**
  - Ctrl+# writes the same code everywhere (`d-mmm-yy` through `NumberFormat`), but the text shown
    follows the regional format.
  - Ctrl+Shift+@ writes a different code under en-US.

  | Regional format | Ctrl+# on A1: `NumberFormat` / `NumberFormatLocal`, text | Ctrl+Shift+@ on A2: `NumberFormat` / `NumberFormatLocal`, text |
  |---|---|---|
  | en-GB | `d-mmm-yy` / `dd-mmm-yy`: **`05-Jan-26`** | `h:mm` / `hh:mm`: **`09:05`** |
  | en-US | `d-mmm-yy` / `d-mmm-yy`: **`5-Jan-26`** | `h:mm AM/PM` / `h:mm AM/PM`: **`9:05 AM`** |
  | ja-JP | `d-mmm-yy` / `dd-mmm-yy`: **`05-1-26`** | `h:mm` / `h:mm`: **`9:05`** |

  - Under ja-JP, the month is shown as a number (`1`), not as `Jan`.
  - Column A stayed at the standard width (8.09) under all three.
  - Excel's `International` values:
    - en-GB: country setting 44, `£`, date order 1.
    - en-US: country setting 1, `$`, date order 0.
    - ja-JP: country setting 81, `¥`, date order 2.
  - The regional format was set back to en-GB afterwards. `HKCU\Control Panel\International` then
    matched the export taken before the run, line for line.

### Seen beside the readings

These answers were not asked for.

- **Ctrl+D, Ctrl+R and the fill handle copy the source's own sides, and leave the edge the source
  shares with the first target alone.**
  - In cases 3 and 5, B2's top edge is `None`. Yet B3's top edge still reads B2's thick bottom
    after the fill: it is the same edge.
  - In case 4, C2's left edge still reads B2's thick red right edge.
  - Neither was set back to `None` by the copy.
- **The fill handle, case 5.**
  - It was found by its cursor. Over B2's middle, the cursor's handle was `0xf6b04e9`.
  - Along the diagonal through B2's bottom-right corner, from 7 px inside to 5 px outside, it was
    `0x67c00767`. Only at 8 px inside was it the cell's cursor again.
  - The press was at (230, 411), next to the corner (231, 412). The drag ended in B4's middle.
  - It filled: B2 kept `1`, B3 and B4 became `1`, and the selection became B2:B4.
- **Row heights.** Every row was 14.5 pt before the cases. Thick bottom edges raised rows to 15 or
  15.5 pt, as in the eleventh run, and the raise followed the edge.
  - **Case 3**, after Ctrl+D: rows 2 to 5 were 15, 15.5, 15.5 and 15 pt.
  - **Case 6:** rows 2 and 3 were 15 pt before the delete.
    - The old row 4 (14.5 pt) became row 3, and read 15 pt.
    - It now has the thick line on its top.
  - **Case 10:** row 1 was 15 pt with the thick line on its top. After the insert, the new row 1
    and row 2 were both 15 pt.
- **Excel's buttons.**
  - **Quick Analysis.** Its button (a `NUIDialog`) showed under the selections B2:B4 (case 3) and
    B2:D2 (case 4).
  - **Insert Options.** After the insert at row 1 (case 10), its button showed at the left of row
    2, inside A2. It is drawn in Excel's own window, and it covers none of the edges read.
- **The standard width** on this machine is 8.09 (Aptos Narrow 11 at 150%).
  - In case 17, `09-Dec-25` widened column A to 8.73.
  - In case 19, `05-Jan-26` fitted 8.09, so its column did not widen.

## Environment

| | |
|---|---|
| Excel | Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), x64; `Application.Build` 20430. English (UK) interface |
| Office Theme | "Use system setting" (File › Account, read through UI Automation; `UI Theme` = 6 in the registry) |
| Windows | Light mode (apps and system); high contrast off; display scale **150%** (144 DPI); work area 3840 × 2088 |
| Regional format | en-GB (`Get-Culture`; `LocaleName` en-GB, `dd/MM/yyyy`, `HH:mm:ss`, `£`); interface en-GB. Case 19 set en-US and ja-JP, then en-GB again |
| Excel's standard font | Aptos Narrow, 11. The standard column width is 8.09 |
| Keyboard | Excel's window was on English (UK), HKL `0x08090809`, with the IME closed (`open=0`), in every case. It was put back to it at the end of every run. The Japanese layout runs over `kbd101.dll`; no case used it |
| Languages | en-GB (0809), en-US (0409), ja (Microsoft IME) |
| Zoom | 100% |
| Gridlines | Excel draws them `#E0E0E0` (sampled). COM's `GridlineColor` is Automatic |

Case 0 (jsonl line 1) typed every character the cases send into A1 (`'~!@#$%^&_=1234.5abc`), and A1
read it back exactly. On UK, `#` is a key without Shift (`OEM_7`), so "Ctrl+`#`" was sent as Ctrl with
`OEM_7`. `&` was sent as Ctrl+Shift+`7`, `@` as Ctrl+Shift+`OEM_3`, `-` as `OEM_MINUS` and `=` as
`OEM_PLUS`.

## Method

The eleventh run's method, unchanged. In brief:

- **Excel.** Each case ran in an Excel of the script's own, started with `New-Object` and checked to
  be a new process.
  - It was ended at the end of the case, by `Quit` or by `Stop-Process` when Quit had not finished
    in 10 s.
  - No running Excel was attached to. The user's AutoRecovered and unsaved workbooks were not
    touched.
  - Each case got a fresh workbook with one sheet, `Sheet1`, maximised at 100%, with A1 in view and
    selected.
- **Input.** Keys went through `SendInput` (a virtual-key and the layout's scan code). Clicks and
  drags were the real mouse.
  - Every operation a case asks about was done with keys, including moving to the cells it names.
  - The fill handle (case 5) and the Format Cells clicks (case 16) were done with the mouse.
- **COM** only set cases up and read results.
- **Pictures** were taken with `PrintWindow` (`PW_RENDERFULLCONTENT`), each window of Excel's laid at
  its place.

**What this run added to the script.**

- **An edge read from both cells.** Every edge a case asks about is read as `LineStyle Weight Color`
  from both cells that share it, for example B2 `xlEdgeBottom` and B3 `xlEdgeTop`. At the Sheet's
  edge, the record says there is no neighbour. A whole row's, column's or Sheet's `Borders` are read
  as well where a case names them.
- **Every operation is read twice.**
  - **`done`:** at once, with the Selection where the operation left it.
  - **`parked`:** after the Selection was moved away from the cells read, with Esc, Ctrl+Home, Down
    13 and Right 6, to G14. The Selection's outline then covers none of the edges. The pixels across
    each edge are read in this state.
- **The line drawn** is read from the pixels across each edge, at a quarter, a half and three
  quarters along it.
  - The pixels run from 6 px before the coordinate `PointsToScreenPixels` gives for it to 6 px
    after, as runs of offset and colour. For example, `-3..-1 000000` is a thick black line across
    the gridline at `-2`.
  - Each state reads its own geometry, because thick lines raise row heights.
- **The far edges of the Sheet** (cases 14 to 16) were pictured by scrolling the window through COM,
  to row 1048560 and to column 16376.
- **The fill handle** (case 5) was found by its cursor, along the diagonal through the Selection's
  corner. The values the drag left show that it filled.
- **Each key in cases 17 to 19** is read safely. If it had opened an edit or a dialog, the state
  would have been read from the picture and the Formula Bar, then Esc pressed. None did.

**Additions to the procedure.** Each is marked in the record.

- **Case 15, passes b and c** (above).
- **Case 17, pass b:** the standard width set to 4, and D1 and F1 given values whose text cannot fit
  (above).
- **Case 18's width of 12** was set through COM (`ColumnWidth = 12`), as a set-up. It is a width that
  is not the standard one, as one set by hand is.

No run stopped. Each case and pass has one line in the jsonl.

## Group 1: the line between two cells, after other operations

Edges are written `LineStyle Weight Color`. "Both" means the two cells that share the edge read the
same. The pixels are from the `parked` state.

| # | Set up | Keys / mouse | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 1 | C2's left thin blue (so B2's right read it). E5's right thick red; E5 = 1 | From A1: Down ×4, Right ×4, Ctrl+C, Up ×3, Left ×3, Ctrl+V | the paste writes C2's left too: thick red, both | **As read.** B2's right and C2's left both read `Continuous Thick #FF0000`. Drawn red across the gridline (`-3..-1 ff0000`); the blue line is gone. B2 = 1 | `1-set-x4.png`, `1-parked-x4.png` |
| 2 | B2's right thick red (C2's left read it). E5 = 1, no borders | As in 1 | none, both | **As read.** B2's right and C2's left both read `None`. The gridline is back (`-2 e0e0e0`) | `2-set-x4.png`, `2-parked-x4.png` |
| 3 | B2 = 1, right thick red, bottom thick black. C3's left thin blue | From A1: Down, Right, Shift+Down ×2 (B2:B4), Ctrl+D | B3 and B4 take B2's right and bottom; C3's and C4's left read thick red | **As read.** B3's and B4's right read thick red, and C3's and C4's left read the same (both). B3's and B4's bottom read thick black, and B4's and B5's top read the same (both). B3's top reads thick black, the edge it shares with B2. B3's and B4's left read `None`. Drawn: red at B3 and B4's right, with the blue gone; black below B3 and B4 | `3-set-x4.png`, `3-parked-x4.png` |
| 4 | B2 = 1, bottom thick black, right thick red. E2's left thin blue | From A1: Down, Right, Shift+Right ×2 (B2:D2), Ctrl+R | C2 and D2 take B2's edges, the neighbours read them, and E2's left reads thick red | **As read.** C2's and D2's bottom read thick black, and C3's and D3's top read it (both). C2's and D2's right read thick red, and D2's and E2's left read it (both). E2's left is thick red, with the blue gone. C2's and D2's top read `None` | `4-set-x4.png`, `4-parked-x4.png` |
| 5 | As in 3 | From A1: Down, Right (B2); B2's fill handle dragged with the mouse to B4's middle | as Ctrl+D | **As Ctrl+D**: every edge read the same as in case 3, and so did the pixels. B3 = B4 = 1 and the selection is B2:B4 | `5-set-x4.png`, `5-parked-x4.png` |

## Group 2: two rows that meet when the row between them is deleted

| # | Set up | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 6 | B2's bottom thick black; B4's top thin blue | From A1: Down ×2, Shift+Space (`3:3`), Ctrl+- | record | **The upper row's line.** B2's bottom and the new B3's top read `Continuous Thick #000000` (both); drawn black (`-3..-1 000000`). The new B3's bottom reads `None` | `6-set-x4.png`, `6-parked-x4.png` |
| 7 | B2's right thick black; D2's left thin blue | From A1: Right ×2, Ctrl+Space (`C:C`), Ctrl+- | record | **The left column's line.** B2's right and the new C2's left read `Continuous Thick #000000` (both); drawn black. The new C2's right reads `None` | `7-set-x4.png`, `7-parked-x4.png` |
| 8 | B2's bottom thick black only | As in 6 | thick black, both | **As read.** B2's bottom and the new B3's top read `Continuous Thick #000000` (both); drawn black | `8-set-x4.png`, `8-parked-x4.png` |
| 9 | B3's top thin blue, set from B3 (B2's bottom read it) | From A1: Down, Shift+Space (`2:2`), Ctrl+- | record | **The line remains.** B1's bottom and the new B2's top read `Continuous Thin #0000FF` (both); drawn blue on the gridline's pixel. The new B2's bottom reads `None` | `9-set-x4.png`, `9-parked-x4.png` |

## Group 3: what insertion leaves on the edges

| # | Set up | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 10 | B1's top thick black (the Sheet's edge) | Shift+Space on A1 (`1:1`), Ctrl+Shift+= | the line goes; the new row 1 takes nothing | **Differs** (the summary). The new B1: top `None`, bottom `Continuous Thick #000000` (both, with B2's top), left and right `None`. B2 (the old B1): top thick black, the other three `None`. `Rows(1).Borders` all `None`. Drawn between rows 1 and 2 (`-2..0 000000`) | `10-set-x4.png`, `10-parked-x4.png` |
| 11 | B2's bottom thick black; B2 filled yellow | From A1: Down ×2, Shift+Space, Shift+Down (`3:4`), Ctrl+Shift+= | B3's top reads the thick line; the edges between the new rows are empty; both new rows are yellow | **As read.** B3's top reads thick black (both, with B2's bottom). B3's bottom and B4's top, B4's bottom and B5's top read `None`. B3 and B4 are filled `#FFFF00` (pattern 1, colour index 6), sampled `#FFFF00`. B5 is not filled. Between B3 and B4 the yellow runs through the gridline | `11-set-x4.png`, `11-parked-x4.png` |
| 12 | B2 = `abc`, bold, italic, red Font, yellow Fill | From A1: Down ×2, Shift+Space, Ctrl+Shift+= | B3 takes the Font and the Fill | **As read.** B3 reads bold, italic, Font colour `#FF0000`, and Fill `#FFFF00` (sampled `#FFFF00`). B4, A3 and C3 are plain | `12-set-x4.png`, `12-parked-x4.png` |
| 13 | B2's right thick black; B2 filled yellow | From A1: Right ×2, Ctrl+Space (`C:C`), Ctrl+Shift+= | C2's left reads the thick line, its right is empty; C2 is yellow | **As read.** C2's left reads thick black (both, with B2's right). C2's right and D2's left read `None`. C2 is filled `#FFFF00`; D2, C1 and C3 are not | `13-set-x4.png`, `13-parked-x4.png` |

## Group 4: outlines over whole rows and the whole Sheet, inside lines over whole columns

| # | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|
| 14 | From A1: Down ×2, Shift+Space (`3:3`), Ctrl+Shift+& | top and bottom only | **Differs** (the summary). `Rows(3).Borders`: left, top and bottom `Continuous Thin`; right and both insides `None`. A3's left `Continuous Thin`. XFD3's right `None`. B3's top and bottom `Continuous Thin` (both). Drawn: the top and bottom lines, black, across the view and at XFD; nothing at A3's left beyond the heading's edge | `14-parked-x4.png`, `14-right-x4.png` |
| 15 | Ctrl+A twice from A1 (`1:1048576`); pass b: the corner box with the mouse. Ctrl+Shift+& | the left of A and the right of XFD only | **Differs** (the summary): nothing was recorded, in all three passes. The pictures at the four corners of the Sheet show the gridlines only | `15-parked-x4.png`, `15-bottom-x4.png`, `15-right-x4.png`, `15-bottom-right-x4.png`, and the same with `15-b-`, `15-c-` |
| 16 | From A1: Right, Ctrl+Space, Shift+Right (`B:C`); Ctrl+1; the Border tab, Inside and OK, each clicked | record | The summary. The B/C edge in row 5 reads `Continuous Thin` (both) and is drawn. B2's top reads `Continuous Thin` (both) and is drawn | `16-parked-x4.png` (rows 1 to 3), `16-bottom-x4.png`, `16-inside-clicked-window-1.png` |

## Group 5: the formatting keys, a column's width, and the built-in date and time formats

Each key was sent as the character on the UK layout, with Right between the cells. Each cell reads
its column's `ColumnWidth` after its key, then the text shown. Columns marked "std" still read
`UseStandardWidth`.

| # | Set up | A1 Ctrl+# | B1 Ctrl+Shift+$ | C1 Ctrl+Shift+! | D1 Ctrl+Shift+% | E1 Ctrl+Shift+^ | F1 Ctrl+Shift+@ |
|---|---|---|---|---|---|---|---|
| 17 | Standard width 8.09; 46000.5, 1234567.5, 1234567.5, 0.123456, 1234567.5, 46000 | **8.73** `09-Dec-25` | **11.82** `£1,234,567.50` | **10.82** `1,234,567.50` | 8.09 std `12%` | 8.09 std `1.23E+06` | 8.09 std `00:00` |
| 17 pass b (added) | Standard width 4; D1 = 1234567.5, F1 = 46000.5 (each showed `####` before its key) | **8.73** `09-Dec-25` | **11.82** `£1,234,567.50` | **10.82** `1,234,567.50` | **10.73** `123456750%` | **7.73** `1.23E+06` | **4.73** `12:00` |
| 18 | As 17, each column at 12 (COM) | 12 `09-Dec-25` | 12 `£1,234,567.50` | 12 `1,234,567.50` | 12 `12%` | 12 `1.23E+06` | 12 `00:00` |

- **Case 17's reading** was "unknown: record which keys widen a standard-width column". The summary
  has the answer.
- **Case 18's reading** was "no column widens". **As read:** no column changed from 12.
- **The codes the keys wrote** were the same in all three runs:
  - `d-mmm-yy` (local `dd-mmm-yy`).
  - `$#,##0.00_);[Red]($#,##0.00)` (local `£#,##0.00;[Red]-£#,##0.00`).
  - `#,##0.00`, `0%` and `0.00E+00`.
  - `h:mm` (local `hh:mm`).
- **Pictures:** `17-standard-<key>-x2.png`, `17-b-narrow-<key>-x2.png` and `18-width12-<key>-x2.png`.

| # | Set up | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|---|
| 19 | Under en-GB, en-US and ja-JP (`Set-Culture`, then a new Excel): A1 = 46027 (5 January 2026), A2 = 545/1440 (09:05), both General | Ctrl+# on A1; Down; Ctrl+Shift+@ on A2 | record | Localised: `05-Jan-26` / `09:05`, `5-Jan-26` / `9:05 AM`, `05-1-26` / `9:05` (the summary's table) | `19-<culture>-set-x4.png`, `19-<culture>-hash-x4.png`, `19-<culture>-at-x4.png` |

## The machine afterwards

- **Excel:** no Excel process is running. Every case's Excel was the script's own.
- **AutoRecover:** `%APPDATA%\Microsoft\Excel` is unchanged. Its three files hash the same as the copy
  taken at the start: `Book1 (version 1).xlsb`, the unsaved `Book1((Unsaved-…)).xlsb` and
  `Excel15.xlb`.
- **Regional format: restored.** It is en-GB, set back at the end of case 19. A new PowerShell reads
  `Get-Culture` as en-GB. `HKCU\Control Panel\International` and `HKCU\Keyboard Layout` match the
  exports taken at the start, line for line.
- **Office's registry changes.** These are Office's own records of the run. No key was added or
  removed, and no value was set back.
  - `HKCU\Software\Microsoft\Office\16.0\Excel`: the previous session's ID, a font-information
    cache, two add-ins' load times, and one document-recovery entry.
  - `…\Common`: the session ID, a telemetry rule list, Excel's experiment configuration (its ETag,
    expiry, configuration IDs and context data), the roaming sync and write times, the services
    cache's system locale, and the update times of the identity and the services catalogue.
- **Keyboard:** the window in front was on English (UK) at the end (`0x08090809`).
