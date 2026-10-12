# Excel readings after the seventeenth run, 2026-10-12

Readings R1 to R8, asked for by the session preparing `0.1.0-rc.1` after the run 17 corrections. None needed the user at
the desk. They use run 17's methods: `excel/xl-server.ps1` (an Excel of its own, real keys and presses through
SendInput, pictures Excel's windows draw of themselves), and Part E's CSV-to-both for ExPivot. **Nothing was decided.**
Each section says what Excel did. Where a reading names its alternatives, it says which one Excel showed. Where
ExPivot was read beside Excel, it says whether the two are the same (**observed**) or differ (**contradicted**).

- **Branch:** `claude/exsheet-windows-readings-2026-10-12`, on `afd70b15`.
- **ExPivot's side (R3):** the DemoHost left running from the seventeenth run (WebAssembly, `http://localhost:5299`),
  built from `8436e45e`, in Chrome 154.0.8037.98.
- **Machine:** the seventeenth run's (Windows 11 Pro 25H2, build 26200, 3840 × 2160 at 150%). Excel 16.0.20430.20146 x64,
  English (UK) user interface.
- **Times:** 16:22 to 17:12 BST.
- **Regional format:** en-GB at the start. Set to en-US at 16:22:09 for R1 to R6, R8 and R3, then put back to en-GB at
  17:09:28 for R7. `HKCU\Control Panel\International` reads the same as before the second area pass exported it (SHA-256
  `071c2b47…456b1af`; `logs/restore-culture.log`). The user had said a change of regional format need not be asked each
  time.
- **Office's display language (R3 (b), (d)):** set to Japanese through Excel's own Options (File > Options > Language >
  Set as Preferred) at 17:01:42. Put back at 17:07, again through Options: "Match Microsoft Windows" set as preferred,
  then English moved up. The list reads as before: `1. Match Microsoft Windows [English] <preferred>, 2. English,
  3. Japanese [日本語]` (`logs/lang-dump-before.log`, `logs/lang-dump-after.log`). Excel had rewritten one value,
  `PreviousUILanguage`, from `en-gb` to `en-GB`. It was set back with `reg add`, and the
  `HKCU\Software\Microsoft\Office\16.0\Common\LanguageResources` export is then byte-identical to the one taken before
  (SHA-256 `828124b6…`). The user had asked, for run 17, for Excel's display language to be changed without them.
- **Left behind:** no Excel is running. Excel's whole-window pictures stay on the machine, because they show the title
  bar's account corner. `shots/` holds crops only (213), cut below the title bar.

**The tools** (`excel/`):

- `xl-server.ps1` is run 17's copy with these additions:
  - `bar`: the Formula Bar's text and its selected part, through UI Automation, with no key sent;
  - `status`: the status bar's words, `Cell Mode Ready` among them;
  - `font`: a Font property set through COM (R6 (5));
  - `pivot`, `fieldlist`, `datafields`: setup for R3 (b) and (d), and the Value Fields' captions and functions read back;
  - `uiaboxes`: names with their boxes, so that the real pointer can press them;
  - its console reads and writes UTF-8, so that a Japanese name reaches it whole.
- The scripts that ran the readings: `r1.py`, `r4.py`, `r6.py` and `r8.py`.
- The measuring tools: `px.ps1` (the colours along a line of a picture), `handle.ps1` (filled squares of #217346, the
  fill handle), `ink.ps1` and `inkruns.py` (a glyph's ink, by column), `colon-render.ps1` (the two colons drawn in a
  font for comparison), `uia-boxes.ps1` and `uia-dlg.ps1`.
- `office-ui-lang.ps1` (run 17's) switched the display language.

**The records** (`records/`):

- `r1.jsonl`, `r4.jsonl`, `r6.jsonl` and `r8.jsonl`, written by those scripts, with every exchange with the server in
  `*-xs.jsonl`.
- `steps-by-hand.jsonl` holds R2, R3 (b) and (d), R5 and R7. They were driven one command at a time
  (`rx.sh <command>`). Every call is there with its full output, read back from this session's transcript by
  `steps-from-session.py`; its times are UTC, one hour behind the BST used here.
- `summary.md` (`summarise.py`) holds R1's, R4's, R6's and R8's tables and R3's cells, generated from the records.

## R1 — the Formula Bar after =, the arrows and F4 (ADR-0051)

A blank workbook with D10 selected. **Each step was its own trial from a cleared sheet.** Then, with no key sent:

- a picture (the bar, the status bar's mode, C9 to E13);
- the bar's text and selection through UI Automation;
- the status bar's `Cell Mode`.

Then the reading Part C used, never with F2: Ctrl+C, then Shift+Home and Ctrl+C. Then Escape until Ready. Every step's
row is in `summary.md`. The pictures are `shots/r1-<case>-<step>-{bar,mode,cells}.png`.

**The press "8 px inside the bar's right end."** The bar's white box ends at x 3827 on this screen. 8 px inside it
(3819, 295) is the bar's own expand button, which spans x 3802 to 3826. That press **expanded the Formula Bar and opened
no edit**: Ready, D10 (`shots/r1-step0-expanded-bar.png`; the button again put it back). The bar's text area ends at x
3801, so every "press in the bar" below is 8 px inside that end, at (3793, 295). Each such press opened an edit: Edit
mode, an empty bar, the keyboard in the bar.

| # | Keys after D10 | Mode | The bar's text (UI Automation) | Selected in the bar | What the picture shows |
|---|---|---|---|---|---|
| (i) | press in the bar, `=`, ↓ | **Edit** | **`=`** | nothing | D10 shows `=`; **no outline on D11** (`r1-i-3-cells.png`) |
| (ii) | the same, then F4, then ↓ | Edit | `=` after F4, `=` after ↓ | nothing | no outline anywhere; F4 and ↓ changed nothing |
| (iii) | press in the bar, `=`, ↓, F4, `+` | Edit | `=`, `=`, `=`, then **`=+`** | nothing | no outline |
| (iv) | press in the bar, `=B2`, F4 | Edit | **`=$B$2`** | **nothing**: a caret at its end, not `$B$2` selected | `r1-iv-3-bar-left.png`: `=$B$2`, no highlight |
| (v) | press in the bar, `=SUM(A1:B2)`, ←, F4 | Edit | **`=SUM(A1:$B$2)`** | **`$B$2`** | `r1-v-4-bar-left.png`: `$B$2` highlighted; A1 untouched |
| (vi) | in the cell: `=`, ↓, Shift+↓, F4 | Enter, Point, Point, Point | `=`, `=D11`, `=D11:D12`, **`=$D$11:$D$12`** | `D11`, `D11:D12`, `$D$11:$D$12` | **both ends cycled**; D11:D12 outlined, dashed (`r1-vi-4-cells.png`) |
| (viii) | in the cell: `=`, ↓, Shift+Home | Enter, Point, Point | `=`, `=D11`, **`=A11:D11`** | `D11`, `A11:D11` | **the outline extends to A11**, the row's first column, and pointing goes on (Point mode) (`r1-viii-3-cells.png`) |

**(vii), in the cell, Enter mode, each from D10 in a cleared sheet:**

| Typed, then | Committed | Selection after | Active cell | D10 holds |
|---|---|---|---|---|
| `abc`, Shift+→ | yes | **D10:E10** | D10 | `abc` |
| `abc`, Shift+↓ | yes | **D10:D11** | D10 | `abc` |
| `=A1+B2`, Shift+← | yes | **C10:D10** | D10 | `=A1+B2` (0) |
| `abc`, Shift+End | yes | **D10** (the status bar shows **End Mode**) | D10 | `abc` |
| `abc`, Ctrl+→ | yes | **XFD10** | XFD10 | `abc` |
| `abc`, Ctrl+Shift+→ | yes | **D10:XFD10** | D10 | `abc` |

**What the clipboard reading did.** Wherever text was selected, Ctrl+C gave the same text UI Automation read:
`$B$2`, `D11`, `D11:D12`, `$D$11:$D$12`, `A11:D11`. With nothing selected it copied nothing. Shift+Home then Ctrl+C:

- **In the bar (Edit mode)** it gave the text from the start to the caret: `=`, `=B2`, `=$B$2`, `=SUM(A1:B2`, `=SUM(A1:`.
- **In the cell (Enter or Point mode)** Shift+Home did not select text. It pointed, and gave `A10:D10` every time.

So in Enter and Point mode this reading changes what it reads. The table above takes the text from UI Automation and
the pictures, taken before the reading.

**Also seen:** while pointing, Excel shows a tooltip with the formula's result beside the cell: `{0;0}` for
`=$D$11:$D$12` and `{0,0,0,0}` for `=A11:D11` (`r1-vi-4-cells.png`, `r1-viii-3-cells.png`).

## R2 — KeyTips (ADR-0100)

**(a) Alt, H, O.** The Format menu of the Cells group opened. Each KeyTip is a window of its own
(`MSO_KEYTIP_WINDOW_CLASS`) whose title is its letter. Read from the windows, and from the picture
`shots/r2-a-format-menu.png`, top to bottom:

| Group | KeyTip | Item |
|---|---|---|
| Cell Size | **H** | Row Height... |
| | **A** | AutoFit Row Height |
| | **W** | Column Width... |
| | **I** | AutoFit Column Width |
| | **D** | Default Width... |
| Visibility | **U** | Hide & Unhide › |
| Organise Sheets | **R** | Rename Sheet |
| | **M** | Move or Copy Sheet... |
| | **T** | Tab Colour › |
| Protection | **P** | Protect Sheet... |
| | **L** | Lock Cell |
| | **E** | Format Cells... |

Three Escapes then left Excel Ready, D10.

**(b) Alt, H, F, C, then one Escape.**

- After Alt, H, F, C, the Font Colour palette was open with two KeyTips: `H` (on its "High-contrast only" switch) and
  `M` (More Colours...) (`shots/r2-b-1-palette.png`).
- After **one Escape**: **the Home tab's KeyTips were shown again**, all 48 of them, from V, X, C, FP and FO to S, FD,
  AS and Y (`shots/r2-b-2-ribbon-keytips.png`). The keyboard was not back in the cell.
- A second Escape showed the tabs' KeyTips (F, H, N, JI, P, M, A, R, W, U2, Y1, Y2, the Quick Access Toolbar's 1 to 4
  and B, and ZC, ZS, ZR, ZP, Q, U1).
- A third left none: Ready, D10.
- Each step's windows are listed in `records/r2.log`.

## R3 — ExPivot beside Excel (ticket 07)

### (a) Item 5 (a)'s other half: every Aggregation over a text field

`part-e/e05c.csv`: `Group,Note / A,x / A,y / B,z / C, / C,`. Rows Group. Value Fields of Note: Average, StdDev, Var,
StdDevp, Varp, Sum, Max, Min, Product, Count and Count Numbers. Built as Part E built its items:

- **Excel**: `part-e-excel.ps1` (run 17's), through COM.
- **ExPivot**: `pivot-probe.mjs` on `/pivot-csv`, which suggested Group and Note as Text.
  - The first run (`records/expivot-r3.json`) read only the eight value columns ExPivot drew in the probe's 1600 px
    viewport.
  - It was run again in a wider window (`pivot-probe-wide.mjs`, viewport 2440 px; `records/expivot-r3-wide.json`), and
    every column was read.

| | Average | StdDev | Var | StdDevp | Varp | Sum | Max | Min | Product | Count | Count Numbers |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A (x, y) | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `2` | `0` |
| B (z) | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `1` | `0` |
| C (two blanks) | empty | empty | empty | empty | empty | empty | empty | empty | empty | empty | empty |
| Grand Total | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `3` | `0` |

- **Observed: every cell is the same in Excel and in ExPivot**, all four rows and eleven columns. That includes C's
  row, where every cell is empty in both, Count included.
- **Three headers differ:**

  | Excel (Value Fields added through COM) | ExPivot |
  |---|---|
  | `Max. of Note` | `Max of Note` |
  | `Min. of Note` | `Min of Note` |
  | `Count of Note2` | `Count Numbers of Note` |

  Excel's own dialog lists `Max.` and `Min.` in this English (UK) edition too (R3 (d)'s pictures).
- Pictures: `part-e/shots/e05c-excel.png`, which shows the columns that fit Excel's window, and
  `part-e/shots/e05c-expivot.png`.

### (b) The Field List's two instruction lines

The Field List, shown over a PivotTable (rows Group, Sum of Amount), was pictured in each language. Each line was
enlarged 4 times from the picture, by nearest neighbour (`shots/r3b-{en,ja}-line{1,2}-x4.png`; the pane
`r3b-{en,ja}-pane.png`).

| Language | Line 1 | Line 2 | UI Automation's names |
|---|---|---|---|
| English | `Choose fields to add to report:` | `Drag fields between areas below:` | `Choose fields to add to report`, `Drag fields between areas below` (no colon) |
| Japanese | `レポートに追加するフィールドを選択してください:` | `次のボックス間でフィールドをドラッグしてください:` | `レポートに追加するフィールドを選択してください`, `次のボックス間でフィールドをドラッグしてください` (no colon) |

- **English:** each line ends with a colon right after the last letter: 1 blank column after `t` and `w`. Its dots are
  2 device pixels wide, 7 px apart.
- **Japanese:** each line ends with a colon as well. Its dots are 2 device pixels wide, 8 px apart.
  - **It begins 4 px after `い`'s last stroke** (3 blank columns): x 3671–3672 after 3667 on line 1, and x 3665–3666
    after 3661 on line 2 (`ink.ps1`, `inkruns.py`).
  - **For comparison**, `colon-render.ps1` drew `さい:` and `さい：` at 144 DPI in Yu Gothic UI and in Meiryo UI, at the
    sizes where さ is as wide as on screen (8 to 9 pt). There U+003A begins 3 to 5 px after `い`, and **U+FF1A begins
    about 10 px after**.
  - **The colon on screen sits where U+003A sits in that comparison.** UI Automation's names leave the colon out, so
    the character itself was not read. A full-width colon that Office draws narrower (proportional metrics) is not
    excluded by this measurement.

### (c) A numeric Row field's Item label in General

`part-e/e16b.csv`: `Key,Amount / 1.23456789012345,1 / 123456789012.345,2`. Rows Key, Sum of Amount. ExPivot suggested
Key as Decimal and Amount as Integer.

| | Excel | ExPivot |
|---|---|---|
| The first Item | **`1.23456789`** (Value2 1.23456789012345, General) | `1.23456789012345` |
| The second Item | **`1.23457E+11`** (Value2 123456789012.345, General) | `123456789012.345` |
| Sum of Amount, Grand Total | 1, 2, 3 | 1, 2, 3 |

- **Contradicted.** Excel's labels are at most eleven characters, as a General cell shows a number: `1.234567890` with
  its trailing zero left out, and `1.23457E+11`. They are the same with the column 40 wide. ExPivot writes fifteen
  significant digits.
- The values agree.
- Pictures: `part-e/shots/e16b-excel.png` and `e16b-expivot.png`.

### (d) Value Field Settings' caption when Count Numbers is chosen in the UI

On a PivotTable with Sum of Amount, the steps were:

1. a right press on a value cell;
2. **Value Field Settings…** (in Japanese **値フィールドの設定...**);
3. the list's first entry pressed, then ↓ six times to **Count Numbers** (in Japanese **数値の個数**).

Then the **Custom Name** box (in Japanese **名前の指定**) was read from the dialog's picture, and OK was pressed.

| Edition | After choosing it | The cell's header after OK | The function COM reads back |
|---|---|---|---|
| English (UK) | **`Count of Amount`** (`shots/r3d-en-2-dialog.png`) | `Count of Amount` | 11 |
| Japanese | **`データの個数 / Amount`** (`shots/r3d-ja-2-dialog.png`) | `データの個数 / Amount` | 11 |

- **Both editions give Count Numbers the same caption as Count.** Choosing 個数 (Count) in the Japanese dialog left
  `データの個数 / Amount` and read back −4112 (`xlCount`).
- The dialog, opened again, still has Count Numbers (数値の個数) selected (`r3d-en-4-dialog.png`,
  `r3d-ja-4-dialog.png`).
- A Value Field added through COM with `xlCountNums` (−4113) also reads back 11, with the same caption
  (`r3d-ja-6-dialog.png`).
- 11 is the number COM gives back for Count Numbers on this Excel. It is also `xlDistinctCount`'s number in the type
  library; the dialog names the choice Count Numbers.
- Through COM in (a), the second Count-like Value Field of Note was captioned `Count of Note2`.
- The English (UK) list reads `Sum, Count, Average, Max., Min., Product, Count Numbers, …`. The Japanese list reads
  `合計, 個数, 平均, 最大, 最小, 積, 数値の個数, …`.

### (e) Part E's items not run (10, 11, 18 to 26, 28)

**Not run.** The request said "if time allows", and none of them gates `0.1.0`.

- Items 19 to 23 and 25 are Field List gestures: ticking, dragging, a field's menu, a double click. They need Excel's
  pane driven by hand and new steps in `pivot-probe.mjs` for ExPivot's side.
- Items 10, 11, 18, 24, 26 and 28 can be read through COM and pictures in Excel. Their ExPivot side needs layouts the
  probe does not build yet: collapse, sort by value, the report filter band, date grouping, and the Design tab's menus.

## R4 — the active cell's border in a selection of several areas (h2, ticket 24)

Excel's zoom at 100% and at 150%, on the 150% display. Each selection was made with the real pointer. The colours were
read along four short lines across the active cell's edges, at its middle (`px.ps1`; every run is in `summary.md`;
pictures `shots/r4-z{100,150}-*-{cells,active}.png`).

| Zoom | Case | Active | The active cell's border | Around it |
|---|---|---|---|---|
| 100% | (1) A1:B2, then Ctrl+drag D4:E6 | D4 | **1 device pixel, #217346**, on every edge, 1 px inside the cell | a 1 px white line outside it, then the area's outline: 1 px **#AFAFAF**; selected cells shaded #C7C7C7 |
| 100% | (2) Enter ×6: D5, D6, E4, E5, E6, **A1** | A1 | 1 DP, #217346 | the same; at A1's left and top, the row and column Headings' 2 px #217346 |
| 100% | (3) A1:B2, then Ctrl+press D4 alone | D4 | 1 DP, #217346 | the same; D4:D4's own outline #AFAFAF |
| 150% | (1) | D4 | **1 DP, #217346** | the same as at 100% |
| 150% | (2) Enter ×6, **A1** | A1 | 1 DP, #217346 | the same |
| 150% | (3) | D4 | 1 DP, #217346 | the same |

- **The border is not #000000.** It is #217346, 1 device pixel wide, at both zooms and in all three cases.
- Enter went through the areas in order: D4, D5, D6, E4, E5, E6, then A1, which is in A1:B2.
- (Run 17's h2 recorded Excel's border as black from a picture at 1×. This reading measured it.)

## R5 — the fill handle on a whole axis (h3)

Pictures: `shots/r5-*-corner*.png`. The handle was found with `handle.ps1`, as a filled square of #217346 at least
5 × 5 px, and measured with `px.ps1`.

| # | Selection | Where the handle is |
|---|---|---|
| (1) | Ctrl+A (`1:1048576`, A1 active) | **none in view**: the only square found is the select-all corner's triangle (`r5-1-ctrl-a-corner.png`) |
| (2) | columns B:D from the Headings (B1 active) | **at the selection's top right**: x 420–424, y 351–356, straddling the Heading's bottom edge over D1's right edge (`r5-2a-corner.png`) |
| (2) | then rows 2:4 from the Headings (A2 active) | **at the selection's bottom left**: x 38–43, y 465–469, on the row Heading's edge under row 4 (`r5-2b-corner.png`) |
| (3) | column C (C1 = 1, C3 = `a`, C20 = `=ROW()`, C1048576 = `z`) | at its top right, **6 × 6 px**, x 324–329, y 351–356 |
| (4) | row 3 (A3 = 1, C3 = `a`, H3 = `=COLUMN()`, XFD3 = `z`) | at its bottom left, x 38–43, y 436–440 |
| (5) | column C, the view scrolled 3 rows down (row 1 out of view; three presses on the scroll bar's down arrow) | **still drawn, at the top right of the part in view**, clipped to **6 × 4 px** (x 324–329, y 353–356) under the Heading (`r5-5b-corner.png`) |

**(3) Column C's handle dragged two columns right, to E:**

- The selection became **C:E**, whole columns. **D and E were filled down the whole column**: D1 `2`, E1 `3`;
  D3 and E3 `a`; D20 and E20 `=ROW()` (20); D1048576 and E1048576 `z`. D2 stayed empty.
- **The lone number in C1 was extended as a series (1, 2, 3).** As a control, a single cell's handle (C5 = 1, dragged
  two rows down) copied it: 1, 1, 1. So the drag holds no Ctrl.
- **Dragged down**, the same handle changed nothing: the selection stayed C:C and no cell changed.
- Pictures: `r5-3b-dragged-right-corner-c.png` and `r5-3c-dragged-down-corner-c.png`.

**(4) Row 3's handle dragged two rows down:**

- The selection became **3:5**, and rows 4 and 5 were filled across: A4 `2`, A5 `3` (a series); C4 and C5 `a`; H4 and
  H5 `=COLUMN()` (8); XFD4 and XFD5 `z`.
- **Dragged sideways**, nothing changed.
- Picture: `r5-4b-dragged-down-corner-c.png`.

## R6 — what widens a column over #### (h7)

Each case was a new workbook. A1 holds 123456789.12 in `0.00`, column A at the standard width (8.09, a width nobody
set), so A1 shows `########`. Then one gesture, read through COM: the font, A1's text and column A's width. Format
Cells' controls were pressed with the real pointer where its picture shows them; the dialog opens on the Number tab
(`shots/r6-*-dialog.png`).

| # | Gesture | Column A after | A1 shows |
|---|---|---|---|
| (1) | Font tab, **Italic** only, OK | **12.45**, a width of its own | `123456789.12` |
| (1) | Font tab, **Underline** Single only, OK | **11.73** | `123456789.12` |
| (1) | Font tab, a **font colour** (Red) only, OK | **11.73** | `123456789.12`, red |
| (2) | **Fill tab**, nothing changed, OK | 8.09, standard | `########` |
| (2) | Fill tab, Yellow, OK | 8.09, standard | `########`, yellow |
| (2) | **Alignment tab**, nothing changed, OK | 8.09, standard | `########` |
| (2) | Alignment tab, Vertical: Top, OK | 8.09, standard | `########` |
| (3) | Home tab's **Bold** button | 8.09, standard | `########` (bold) |
| (3) | Home tab's **Italic** button | 8.09, standard | `#######` (italic: one # fewer) |
| (3) | Home tab's **Underline** button | **11.73** | `123456789.12` |
| (4) | **Ctrl+B**, then **Ctrl+B again** | 8.09, standard, after each | `########` after each (bold, then not) |
| (5) | through COM, `Range.Font.Bold = True` | 8.09, standard | `########` |
| (5) | through COM, `Range.Font.Underline = 2` | **11.73** | `123456789.12` |

- **OK on the Font tab widened the column** for each of the three changes.
- **Underline widened it however it was set**: the Font tab, the button and COM. Run 17 found the same for Ctrl+U.
- **Bold and Italic set by the buttons, by Ctrl+B or through COM did not widen it.**
- **The Fill and Alignment tabs did not widen it**, changed or not.
- Run 17's h7 found the Font tab's Bold widening it.

## R7 — Custom codes under the en-GB regional format (h9)

A new workbook under en-GB. Each code was typed in Format Cells' Custom Type box, then OK
(`shots/r7-*-dialog.png`; the Sample in each).

| # | Value | Code | Sample in the dialog | The cell shows |
|---|---|---|---|---|
| (1) | 0.37847222 | `h:mm` typed | `9:05` | **`9:05`** (not `09:05`) |
| (2) | 46027 | `d-mmm-yy` typed | `5-Jan-26` (`r7-2-sample.png`) | **`5-Jan-26`** (not `05-Jan-26`) |
| (3) | 46027 | the Date category's built-in d-mmm-yy | — | **not offered** |

**(3)** Under Locale (location) English (United Kingdom), the Date category lists nine types and none is d-mmm-yy:

- `*14/03/2012` and `*14 March 2012`;
- `14/03/2012`, `14/03/12`, `14/3/12`, `14.3.12` and `2012-03-14`;
- `14 March 2012` (Sample `05 January 2026`) and `14 March 2012` (Sample `5 January 2026`).

The pictures are `r7-3-dialog.png`, `r7-3-list-*-c.png` and `r7-3-list-pgup-*-c.png`. An attempt to choose English
(United States) in the Locale list by typing landed on Nepali (`r7-3-locale-us-dialog.png`). It was cancelled and not
pursued, so A3 was left in General.

## R8 — Format Cells' Border tab on opening (h10)

Each case was a new workbook. The lines were set through COM on each cell's own edges. Then the cells were selected,
Ctrl+1 pressed, and Ctrl+Tab three times went to the Border tab (`shots/r8-*-dialog-c.png`, the Style list enlarged in
`r8-*-style.png`).

| # | Cells | Selected in Style on opening | Colour on opening |
|---|---|---|---|
| (1) | B2 with a **thin top** and a **thick right** of its own | **the thin solid line** (the left column's last entry), with the focus rectangle | **Automatic** |
| (2) | A2:B3: A2 thin top, B2 medium right, A3 thick bottom, B3 dashed left | **the thin solid line** | **Automatic** |
| (3) | D5, no line | **the thin solid line** | **Automatic** |

- In each case Escape left every cell's lines as they were (`records/r8.jsonl`).
- Case (1)'s preview shows the top thin and the right thick. Case (2)'s shows every outer edge and both inside lines
  as grey hatching, with no edge button pressed.
- Run 17's h10 opened on **Thick** over a cell whose top showed the thick bottom of the cell above. None of these three
  opened on Thick.

## Left for the user at the desk (not started)

Narrator (Part F); NVDA's row reading (Ctrl+↓ to the last row on `/sheet`; a tenth row and the last row on `/cells`);
NVDA's silence on the first pass, asked three ways; the glyph measurement under Japanese display (sign out, display
language Japanese, `measure.mjs` in Chrome and Edge, then back), which the user agreed to before rc.1; ED-30's k8 with
the IME's text suggestions off, in the eighteenth run.

## Nothing for the Docs Site

This branch adds a verification record only.
