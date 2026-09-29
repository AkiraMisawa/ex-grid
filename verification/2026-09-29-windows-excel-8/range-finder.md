# Verification — 2026-09-29, Windows, Excel's range finder (eighth run, Part A)

**Scope: Part A of [`verify-on-windows-8.md`](../../docs/specs/exsheet/verify-on-windows-8.md)**, what
Excel draws while a Formula is open in an edit, asked for the readings of
[ADR-0057](../../docs/adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md). Part B
was not run: it waits for tickets 27–30.

**Verified commit: `7628ab9f03ceccbb60bc8846ca5822fb073a53ca`**, the tip of
`claude/exsheet-reference-outlines` when this run began. The results are committed on
`claude/exsheet-windows-verify-8`, branched from that commit. Part A asks Excel only, so nothing was
built. Nothing here changes an ADR, `CONTEXT.md` or the Definition of Done.

Files beside this one:

- `range-finder.ps1`: the script, one case or a list (`-Case 1,2`), and `-Analyse` to read the saved
  screenshots again.
- `range-finder.jsonl`: one line per case (the environment is case `0`), holding:
  - the geometry, read through COM before the edit;
  - the Formula Bar's content, read through UI Automation in each state;
  - the pixel readings.
- `shots/`: for each state:
  - `A<case>-<state>-window.png`, the window at 600 ms, with the account's initials blanked;
  - crops of the Formula Bar (`-formula-bar`), of row 10 from D10 rightwards (`-cell`), and of the
    cells the case names, at 600 ms (`-cells`), at 900 ms (`-cells-900ms`) and before the keys
    (`-before-cells`);
  - `Z*.png`, enlargements with no smoothing, used for the readings below.
- `type-probe.ps1`, `type-probe-2.ps1`: the checks behind the change of key method (below).
  `zoom-crop.ps1` makes the enlargements.

## Environment

- The machine of the earlier runs: Windows 11 Pro 25H2 (build 26200.9457) with one display, 3840×2160 at
  150% (144 dpi).
  - Windows is in **light mode** (`AppsUseLightTheme` 1, `SystemUsesLightTheme` 1), and high contrast
    is off.
  - The regional format is en-GB. This run did not change it.
- **Excel: Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, 64-bit**
  (16.0.20430.20092). This is a newer build than the fifth run's 16.0.20326.20158: the System log
  shows Office's packages re-registered at 23:29:55.
  - **Office Theme: "Use system setting"** (File › Account, read through UI Automation;
    `shots/A0-office-theme.png`). There is no `UI Theme` value in the registry.
  - Settings: Edit directly in cell on, Formula AutoComplete on, the standard font Aptos Narrow 11, and
    the window at 100% zoom unless a case says otherwise.
- **The keyboard.**
  - For each run, Excel's window was switched from the Japanese keyboard (0x04110411) to **English
    (UK) (0x08090809)** by `WM_INPUTLANGCHANGEREQUEST`. The script's own thread was switched too,
    because characters are turned into keys through the sender's layout.
  - The keyboard was switched back after each run (three times, each back to 0x04110411), with the IME
    turned off.
  - Case 0 typed `'=Sheet1!A1+$B$2:C3,"x"&(D4)[E]a` into D10. It read back **the same**.
- **Excel was the script's own.** Each run started one with `New-Object` (never `GetActiveObject`) and
  ended it with `Quit`.
  - At about 23:25, an Excel that was not this run's was open, with an unsaved workbook. By 23:34, before
    the first run started, it was no longer running. Nothing in this run attached to it.
  - The AutoRecovered folder in `%APPDATA%\Microsoft\Excel` was backed up first. It is **byte for byte
    unchanged**.
- Times. The recorded run of cases 0–23 went from 23:59:25 to 00:01:08. The two additions went from
  00:04:17 to 00:04:28 (`1fb`) and from 00:05:59 to 00:06:10 (`20x`). The attempts between 23:34 and
  23:53 are described below. No Excel was running afterwards.

## How this run differs from the procedure's method

1. **Keys went through `SendInput`, not `SendKeys`.**
   - From 23:36, keys sent by `SendKeys` reached Excel garbled, and differently on every try:
     - `=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1` arrived as `[-j+Akt~o/GlpB*.Enc_[q-fylv+As`;
     - the keyboard check arrived as `'=Sb-ad#>+8c?|{eG*t;^bWZ$+i<cYhf`.
   - `type-probe.ps1` typed `abcdefghij1234567890` into a fresh Excel. Only `SendInput` delivered it
     intact:

     | Sent through | Keyboard | Arrived as |
     |---|---|---|
     | `SendKeys` | Japanese, as in the earlier runs | `al-pbcde0mfgn4hqit/1` |
     | `SendKeys`, one character at a time | Japanese | `3*rj+xa2\-kopl5bc[m,` |
     | `keybd_event` with scan codes | Japanese | `g67defghid0ha4567890` |
     | `SendKeys` | English (UK) | `hid47qefg/0*jnt2-+8a` |
     | `keybd_event` | English (UK) | `0bccghiab3163d56e991` |
     | `SendInput` (`type-probe-2.ps1`), three ways (scan codes only, virtual-key and scan code, Unicode), at two speeds | Japanese | intact, 9 of 9, a string with every character the cases use |

   - **Every key of the recorded run went through `SendInput`**: a key down and a key up per key, with
     its virtual-key code and its scan code, and Shift where the English (UK) keyboard needs it.
   - In every state of every case, **the Formula Bar's content, read through UI Automation, is exactly
     the keys typed** (`formulaBarHolds` in the jsonl).
   - Why `SendKeys` and `keybd_event` failed was not found.
2. **Screenshots are the window's own rendering** (`PrintWindow` with `PW_RENDERFULLCONTENT`), not a
   copy of the screen.
   - From 23:36, `CopyFromScreen` returned every pixel as 0, alpha included, including after a wake
     request (`SC_MONITORPOWER`).
   - Windows then reported the monitor off line (`Win32_DesktopMonitor` availability 8), while WMI
     reported its connection active.
   - The window's rendering was live. In most states the text caret blinked between the two captures
     taken 300 ms apart (`movedIn300ms`).
   - A window capture holds Excel's main window only. Excel's popups (the AutoComplete list, and
     ScreenTips such as the argument hint after `=SUM(A1,`) are windows of their own and are not in
     the shots.
3. **The procedure's sampling, 1 to 3 px inside each edge, lands on the fill, not on the line.**
   - At this display, Excel draws the outline's line on the gridline and 1 px outside the cell's box.
     Then comes a white gap of 1 px, then the pale fill.
   - The jsonl keeps the procedure's reading as `inside1to3`: the most frequent colour there that is
     neither the cell's ground nor the gridline. **It is the fill's colour.**
   - Beside it are:
     - `line`: the saturated colour most often found across the edge, from −3 to +3 px;
     - `lineAt`: how much of the edge carries that colour at each distance, and in how many runs;
     - `profile`: the pixels across the edge's middle, from −4 to +6 px.
   - **An outline's cells were read from the fill.** Cells that took the same pale colour, and touch,
     make one outline. The line colours on their edges are listed with them.
4. **Text colours were read by the darkest pixel of each column.** The saturated pixels the procedure
   names include ClearType's coloured fringes on black text.
   - The core of a coloured stroke wears the text's colour exactly.
   - A column whose darkest pixel is saturated (max − min > 60) is coloured.
   - Columns of one colour make a run. Runs two columns wide or less are fringes, and are counted but
     not listed.
   - Which characters each run covers was read from the crops.
5. **Two cases are additions, not in the procedure**, and are marked as such:
   - `1fb`: case 1 typed into the Formula Bar, for the Formula Bar's colours;
   - `20x`: case 20 with the second pointing moved on to D12, because case 20's keys point at D11 twice.
6. Only the crops, the 600 ms window and the enlargements are committed. The full screenshots taken
   before the keys and at 900 ms stay on the Windows machine, in
   `%LOCALAPPDATA%\exgrid-layer3\range-finder-8\`.
7. The attempts before the recorded run:
   - 23:34: an error in the script;
   - 23:35 and 23:36: blank screenshots and garbled keys;
   - 23:39: garbled keys;
   - 23:52: case 0 and case 1 with `SendInput`, whose shots the recorded run replaced.

   Nothing from them is in the results.

## What Excel draws

Common to every case.

- **An outline is a line, a white gap and a fill:**
  - a **2-px line** in the Reference's colour, on the gridline and 1 px outside the cell's box;
  - inside it, a **1-px white gap**;
  - then a **pale fill** of the colour over every cell of the Reference;
  - at each corner of the Reference, a **5×5-px square** in the colour, with a 1-px white margin.
- **At 400% zoom the line, the gap and the squares keep their size in screen pixels** (case 23):
  2 px, 1 px and 5 px.
- **Where two outlines meet, the later one covers the shared edge.** In case 1, A1's right edge shows
  B1's red line, not A1's blue (`Z01`). In case 6, B2's red outline covers the blue outline of A1:B2
  along B2's edges (`Z06`).
- **Some lines do not show:**
  - in row 1 the top line, and in column A the left line, lie under the headings' border;
  - around the cell being edited, the active cell's green border covers the line (case 22).
- **The text is coloured only in the surface being edited:**
  - while the edit is in the cell (typed, F2, a double-click), the cell's text is coloured, and **the
    Formula Bar's text is black**;
  - while the edit is in the Formula Bar (case 21's last state, and `1fb`), the Formula Bar's text is
    coloured, and the cell's text is black.
- **The palette has seven colours**, taken in order of first appearance, then round again. The Formula
  Bar's colours are other shades of the same seven:

  | # | Line, corner squares and the cell's text | The Formula Bar's text | Fill |
  |---|---|---|---|
  | 1 | `#326ac7` blue | `#006cbe` | `#ebf0f9` |
  | 2 | `#c0353e` red | `#bc2f34` | `#f9ebec` |
  | 3 | `#8157b7` purple | `#7c53ac` | `#f2eef8` |
  | 4 | `#007c20` green | `#0f700f` | `#e6f2e9` |
  | 5 | `#b03e84` magenta | `#bf0077` | `#f7ecf3` |
  | 6 | `#b64900` orange | `#b5490f` | `#f8ede6` |
  | 7 | `#267392` teal | `#00758f` | `#e9f1f4` |

- **Each Reference draws its own outline**, even when another Reference names the same cells. The
  fill is then laid twice: A1 is `#d9e2f4` under `=A1+A1`, against `#ebf0f9` under `=A1`. The same
  happens where two outlines overlap (B2 in case 6, `#e7dde7`).
- **Point.** The Reference being pointed at has its outline in its colour: fill, corner squares and
  line.
  - Over the line runs **a dashed line in green (`#217346`, the colour of the active cell's border)**,
    2 px wide. The dashes are about 7 px long, with gaps of about 2 px: 11 dashes along an 88-px edge.
  - Nothing but the caret changed between the two captures 300 ms apart.
  - Once the Formula holds more than one Reference, the pointed Reference's text is shown selected: a
    grey ground, the text darkened (cases 20 and `20x`).

## The cases

"Agrees" compares Excel's answer with the procedure's reading. Colours are from the 600 ms capture.

| # | Set up (COM) | Keys typed into D10 | What is asked | Reading | Excel | Agrees |
|---|---|---|---|---|---|---|
| 0 | a fresh workbook | `'=Sheet1!A1+$B$2:C3,"x"&(D4)[E]a` and Enter | the environment, and the keyboard | — | Read back unchanged. Environment above | — |
| 1 | — | `=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1` | The colour of each Reference, in order. After how many the colours repeat | eight colours, then round again | In the cell and on the outlines: A1 `#326ac7`, B1 `#c0353e`, C1 `#8157b7`, D1 `#007c20`, E1 `#b03e84`, F1 `#b64900`, G1 `#267392`, **H1 `#326ac7`** (as A1), I1 `#c0353e`, J1 `#8157b7`. **Seven colours, then round again.** Each outline is solid with a fill and corner squares. The Formula Bar is black | **No**: seven, not eight |
| 1fb | — (not in the procedure) | the same, typed into the Formula Bar after a click at (448, 295) | the Formula Bar's colours | — | Formula Bar: `#006cbe`, `#bc2f34`, `#7c53ac`, `#0f700f`, `#bf0077`, `#b5490f`, `#00758f`, then `#006cbe`, `#bc2f34`, `#7c53ac`. Seven again, in the same order. The outlines as in case 1; the cell's text black | — |
| 2 | — | `=A1+A1` | One colour or two; one outline or two | one colour, one outline | **One colour**: both `A1` texts `#326ac7`. One outline at A1, **drawn twice**: its fill is `#d9e2f4`, against `#ebf0f9` for one Reference | Colour yes. The outline shows once, but is drawn once per Reference |
| 3 | — | `=A1+$A$1` | The same | one colour, one outline | As 2: `A1` and `$A$1` both `#326ac7`; A1's fill `#d9e2f4` | As 2 |
| 4 | — | `=A1+B1+A1` | The colour of each of the three | A1 first colour, B1 second, the second A1 first | `#326ac7`, `#c0353e`, `#326ac7`. A1's fill `#d9e2f4` (two layers) | Yes |
| 5 | — | `=B2:A1` | The outline's cells | A1:B2 | One outline over **A1:B2**. The fill is on A1, B1, A2 and B2. The line runs along the outer edges (C1 and C2 on the left; A3 and B3 on top), and there is none between the four. The text `B2:A1` stays as typed, one run of `#326ac7`, colon included | Yes |
| 6 | — | `=A1:B2+B2` | Colours and outlines | two colours, two outlines | A1:B2 is blue (`#326ac7`; fill on A1, B1, A2). B2 is red (`#c0353e`), with its own line and corner squares, drawn over the blue one; its fill `#e7dde7` is both fills laid together (`Z06`) | Yes |
| 7 | — | `=A1+B1`, then F2, `{HOME}{RIGHT}{DEL 3}` (leaving `=B1`) | B1's colour before the deletion and after | first colour after: colours follow first appearance | Before: A1 `#326ac7`, B1 `#c0353e`. After (`=B1`): **B1 `#326ac7`**, its outline blue, and A1 no longer outlined | Yes |
| 8 | — | `=Sheet1!A1` | Coloured? Outlined? | both | The whole of `Sheet1!A1`, the sheet's name included, is one run of `#326ac7`. A1 is outlined | Yes |
| 9 | Add a sheet `Sheet2`, then activate Sheet1 | `=Sheet2!A1+B1` | Is `Sheet2!A1` coloured? B1's colour | `Sheet2!A1` uncoloured, B1 first colour | `Sheet2!A1` is black. **B1 is `#326ac7`** and outlined. A1 on Sheet1 has no outline | Yes |
| 10 | — | `=SUM(A:A)` (10a); then `=SUM(1:1)` (10b) | The outline's extent | the whole column; the whole row | 10a: A1, A30 and A57 (the last whole row in view) all wear the fill `#eaf0f9`. The line runs down the right of column A; the top is under the heading, and the bottom is below the view. 10b: A1, H1 and AM1 (the last whole column in view) wear it, with the line along the bottom of row 1. The text `A:A` and `1:1` is `#326ac7` | Yes (as far as the view shows) |
| 11 | A1:B4 as a Table `Positions`, headers `Id`, `PV`, then 1, 2, 3 and 100, 250, 75 | `=SUM(Positions[PV])` | Coloured? Which cells are outlined | coloured; B2:B4 | `Positions[PV]` is one run of `#326ac7`, brackets included; `SUM(` and `)` are black. Only **B2, B3 and B4** changed ground (`#c0e6f5`→`#b2d9f0` on the banded rows, white→`#ebf0f9` on B3). The line runs round B2:B4. B1 (the header) and column A are untouched | Yes |
| 12 | As 11 | `=SUM(Positions[PV])+SUM(Positions[Id])` | Two colours? | two | `Positions[PV]` is `#326ac7`, outlined over B2:B4. `Positions[Id]` is `#c0353e`, outlined over A2:A4 (fills `#c0d4e3` on the banded rows, `#f9ebec` on A3; the line red) | Yes |
| 13 | — | `=SUM(A1,` | Is A1 coloured and outlined? | yes | `A1` is `#326ac7`, and A1 is outlined. (Excel's argument ScreenTip is a window of its own and is not in the shot) | Yes |
| 14 | — | `=A1+` | The same | yes | `A1` is `#326ac7`, and A1 is outlined | Yes |
| 15 | — | `="A1"&B1` | Is the `A1` inside the string coloured? | only B1 | `"A1"` is black. **B1 is `#326ac7`** and outlined. A1 has no outline | Yes |
| 16 | — | `=LOG10(A1)` | Is `LOG10` coloured, or cell LOG10 outlined? | only A1 | `LOG10` is black. `A1` is `#326ac7`, and A1 is outlined. (The cell LOG10 is out of view; nothing else in view is outlined) | Yes |
| 17 | — | `=a1` | Coloured? | yes | `a1` stays lower case while the edit is open, and is `#326ac7`. A1 is outlined | Yes |
| 18 | — | `A1` (no `=`) | Anything coloured or outlined? | nothing | Nothing: the text is black and there are no outlines | Yes |
| 19 | — | `=`, then `{DOWN}` (pointing at D11) | The pointed outline: colour, dashed or solid, moving or still | dashed, first colour | The text is `=D11`, with `D11` `#326ac7`. D11 has the first colour's fill (`#ebf0f9`), corner squares and line, and over the line a **green dashed line (`#217346`)**. It was **still**: between the captures at 652 and 1014 ms only the caret changed (`Z19`) | Dashed and still yes. **The dashes are green, not the first colour**; the fill, squares and line under them are the first colour |
| 20 | — | `=`, `{DOWN}`, `{+}`, `{DOWN}` | The first outline once pointing has moved on, and the new one | the first solid; the new one dashed, second colour | **The second `{DOWN}` points at D11 again**: Point starts from D10 after the `+`. The Formula is `=D11+D11`. Both Refs are `#326ac7`, and D11's fill is `#d9e2f4` (two layers), with the green dashes over it. The pointed `D11` in the text is selected (grey ground, text `#0401a2`) | **No, for the keys as written**: the new Reference names the same cell and takes the **first** colour. See `20x` |
| 20x | — (not in the procedure) | `=`, `{DOWN}`, `{+}`, `{DOWN}{DOWN}` (`=D11+D12`) | as 20, with the pointing moved on | — | **D11: solid blue, with no dashes. D12: the second colour** (`#c0353e` line and squares, fill `#f9ebec`), with the green dashes over it. The pointed `D12` in the text is selected (grey ground, `#630101`) (`Z20x`) | Agrees with 20's reading, except that the dashes are green |
| 21 | `=A1+B1` written into D10 through COM | (1) D10 selected, no edit; (2) F2; (3) Escape, then a double-click at (377, 629), D10's middle; (4) Escape, then a click at (448, 295), in the Formula Bar's text. The Formula Bar is UI Automation's `XLFormulaBarEditor`, 328,267–3840,324 | Outlines shown in each state | none when only selected; shown for F2, the double-click and the Formula Bar | (1) **none**; (2) A1 blue, B1 red, the cell's text coloured, the Formula Bar black; (3) the same; (4) A1 blue, B1 red, **the Formula Bar's text coloured `#006cbe`, `#bc2f34`**, and the cell's text black (`Z21`) | Yes |
| 22 | — | `=D10` | Is D10, the cell being edited, outlined? | yes | `D10` is `#326ac7` in the text. D10 has **blue corner squares at its four corners**. Along its edges is the active cell's green border (solid), with no blue line showing and no fill: the in-cell editor's ground (`Z22`) | **In part**: the corner squares only |
| 23 | Zoom 400% | Case 1's keys | One outline close up: its width in screen pixels, the fill, the corner squares | solid, a pale fill, corner squares | Solid. The line is **2 px**, the white gap 1 px, and the corner squares **5×5 px**, the same as at 100%. The fill is `#ebf0f9`. The colours are as case 1's (`Z23`) | Yes |

## Where Excel and the readings differ

Each item names the paragraph of ADR-0057 it bears on. Nothing is decided here.

1. **The palette has seven colours, not eight** (cases 1, 23, `1fb`). "Readings" says "The palette has
   eight colours". Excel's seven are listed above, in order.
2. **The text is coloured only in the surface being edited**, and the Formula Bar's shades differ from
   the outlines' (cases 1–23 in the cell; case 21's last state and `1fb` in the Formula Bar). "What
   shows, and when", first bullet, says "Its text wears that colour in the Cell Editor and in the
   Formula Bar."
3. **Point's dashes are green (`#217346`), not the Reference's colour** (cases 19, 20, `20x`). They lie
   over an outline that is in the Reference's colour: fill, corner squares and line. "Readings", last
   bullet, says "Point's outline is dashed, in its Reference's colour". "What shows, and when" says
   "Point's outline is the Reference Outline of the Reference it is writing. It takes that Reference's
   colour." Dashed, and still between the two captures, agree.
4. **The same cells named twice are outlined in one place and one colour, but drawn once per
   Reference**, so the fill is laid twice (`#d9e2f4` against `#ebf0f9`; cases 2, 3, 4, 20). "Readings"
   says "`=A1+A1` and `=A1+$A$1` each outline A1 once, in one colour". One colour agrees.
5. **Case 20's keys give `=D11+D11`**, so the new Reference takes the first colour, not the second.
   The table's reading for 20 expected the second. This case bears on "What shows, and when" (Point's
   outline takes its Reference's colour) and on "Readings" (the same cells share a colour). With the
   pointing moved on to D12 (`20x`), Excel does what the reading says, except that the dashes are green
   (item 3).
6. **The cell being edited shows its outline's corner squares only** (case 22). The line lies under the
   active cell's green border, and the fill under the editor's ground. The procedure's reading was
   "yes". ADR-0057 says nothing of this cell.

Also recorded here, though no reading names them:

- the corner squares;
- the white gap;
- the line lying outside the cell's box;
- the later outline covering a shared edge;
- the lines under the headings;
- the pointed text shown selected.

## The machine afterwards

- No Excel is running.
- The AutoRecovered folder is byte for byte the backup taken first.
- Excel's keyboard was switched back to 0x04110411 after each run, with the IME off.
- The regional format is en-GB, unchanged.
