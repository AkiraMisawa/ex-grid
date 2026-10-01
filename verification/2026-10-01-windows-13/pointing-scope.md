# Verification — 2026-10-01, Windows, the thirteenth run: Excel at a value-list argument and after a heading, and ExSheet beside ADR-0058's decisions

**Scope: Parts A and B of `docs/specs/exsheet/verify-on-windows-12.md` as it stood on
`claude/exsheet-ninth-run-b` at the verified commit**, run as the thirteenth Windows run.

- **Why "thirteenth".** The Cell Format line has a twelfth run of its own, with its own
  `verify-on-windows-12.md` on `claude/exsheet-cell-format`, and `claude/exsheet-windows-verify-12`
  holds its Part A. On the Pointing Scope session's word, this run was numbered thirteenth, and its
  results are on `claude/exsheet-windows-verify-13`.
- **The procedure.** That session is renaming the procedure on `claude/exsheet-ninth-run-b` to
  `verify-on-windows-13.md`, with the same cases. This run followed the file as it stood at the
  verified commit.
- **What it asks.**
  - Part A asks Excel what ADR-0058, "What Part B of the ninth Windows run settled", left to it (Q49,
    Q50, Q52).
  - Part B types that section's decisions into ExSheet (Q49, Q51, Q52, Q53; tickets 55 to 58).

**Verified commit: `6ebc1861fa8546d9e1992d1a2d45c59462b40772`**, the tip of `claude/exsheet-ninth-run-b`
when this run began. Tickets 55 to 58 say `Status: done` there. Nothing here changes an ADR,
`CONTEXT.md` or the Definition of Done. **Nothing was decided.**

Files beside this one:

- `excel/ask-excel.ps1`: Part A's script.
  - It is Part B of the ninth run's script (`../2026-10-01-windows-9/excel/ask-excel.ps1`), with this
    run's cases. Its keyboard check also types `*`, `"` and a space.
  - `excel/ask-excel.jsonl` holds one line per case.
  - `excel/shots/` holds the pictures, with the account's initials blanked.
- `pointing-scope-probe.mjs`: Part B's probe, the ninth run's with this run's pages and cases.
  - Playwright opens the page, reads the DOM and takes the page's pictures. It sends no input.
  - Every key, press, drag and turn of the wheel goes through `input-server.ps1`, which is the ninth
    run's, unchanged but for its header comment.
- `summarise.py`: one line per page, case and state, then whether every configuration read the same.
  It is the ninth run's, and it also reads the additions and whether the dashes lie inside the client
  area across. `summary.txt` is its output.
- `records/<page>-<host>-<browser>.json`: one record per page and configuration. `extra-*.json` holds
  the additions (below).
- `shots/`: the page's own picture of the Sheet and the grid beside it, at 600 ms, for every state of
  WebAssembly under Chrome (`<page>-<case>-<state>-wasm-chrome.png`). The other configurations'
  pictures stay on the machine.

## Environment

- **The machine of the earlier runs.**
  - Windows 11, one display, 3840×2160 at **150%** (144 dpi; `devicePixelRatio` 1.5 on every page).
  - Light mode (`AppsUseLightTheme` 1, `SystemUsesLightTheme` 1), high contrast off, regional format
    en-GB.
- **Excel**: Microsoft 365, **Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, 64-bit**,
  the build of the eighth to twelfth runs.
  - Office Theme "Use system setting" (File › Account; `UI Theme` 6).
  - Edit directly in cell, Formula AutoComplete and Function ScreenTips are on. "Use table names in
    formulas" is on.
- **The keyboard.**
  - Each Excel's window was on English (UK), 0x08090809, before the script posted it, and was put back
    to that afterwards. The IME was off.
  - **Excel's keyboard check (case 0) read back exactly, case and all.** It typed
    `'=SUM(Positions[PV])+XLOOKUP(1,A2:A4,B2:B4,,0,-1)*AVERAGE(10,"X 5")` and read the same.
  - Every browser window was on 0x08090809 before and after its run. The probe's `english` request
    found it so. `VK_IME_OFF` was sent once per run.
- **Chrome 153.0.8010.54 and Edge 154.0.4258.48**, as installed, headed.
  - The window was 1600×1050 DIP at the display's own scale (`viewport: null`; the page 1586×956 CSS
    px).
  - Edge is a newer build than the ninth run's (154.0.4258.37).
  - They were driven by Playwright 1.62.1 under the portable Node v24.14.1, from a copy of
    `tests/ExGrid.Browser` at the verified commit (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-01-13`),
    with `npm ci` run afresh in it.
  - A Chrome of the user's was open throughout. Playwright's Chrome is a process of its own, with its
    own profile.
- **Both DemoHosts ran in WSL from the verified commit's build** (`dotnet build ExGrid.slnx`: 0
  warnings, 0 errors): WebAssembly on `localhost:5299`, Server on `localhost:6298`.
- **The 150 ms configuration** reached the Server host through `tests/ExGrid.Browser/latency-proxy.mjs`,
  run on Windows (5298 → 6298, control on 7298).
  - The probe set `rtt=150` before its first key and `rtt=0` after its last.
  - Before each press that follows `=`, it waited until the positions grid wore `ex-pointed-at`, as the
    procedure asks.

## Method

1. **Excel** (`excel/ask-excel.ps1`), 15:31:54–15:36:19: as Part A below says.
   - Every one of the 18 Excels quit when asked, the cases with Book2 too (the ninth run's Book2 cases
     had needed `Stop-Process`).
2. **Six browser configurations**, run one after another, 15:42:32–15:51:39:
   - WebAssembly, Chrome and Edge;
   - Server, Chrome and Edge;
   - Server behind the 150 ms proxy, Chrome and Edge.

   In each, the three pages were run, each in one load, and each page's additions straight after it.
   - `/sheet` was waited for until B12 showed `318.25` (the Linked Table's first push).
   - `/pointing` and `/pointing?narrow` were waited for until B1 showed `8200`.
   - Screen pixels were calibrated against the page's own `mousemove`.
   - Part A's groups 1 and 2, typed into ExSheet (an addition), followed in every configuration,
     15:51:47–15:56:53.
3. **Each case starts with no edit open, and the Sheet's target cell selected and empty**: D10 on
   `/sheet`, C3 on `/pointing`.
   - Keys go one every 30 ms.
   - A press is the real mouse at the middle of the cell or the header. A drag moves in 20 steps of
     15 ms.
   - Behind 150 ms, before each press that follows `=`, the probe waited until the positions grid wore
     `ex-pointed-at`. That took 126–147 ms.
4. **The narrow view's set-ups** use the real wheel over the grid's body, which gives the grid no Focus.
   - b10a: the horizontal wheel turned left 10 times. The grid was already at its first column
     (`scrollLeft` 0, `scrollTop` 0).
   - b11: the wheel turned down 15 times, then the horizontal wheel right 10 times (`scrollTop` 880,
     `scrollLeft` 52, both at their ends).
   - The pointer then left the grid.
5. **Each state is read 600 ms after its last input** (900 ms behind the 150 ms proxy). None had to wait
   longer for the layer of the field with focus to catch up.
   - From the DOM: as the ninth run read it. That covers where DOM focus is and where it went, the
     Cell Editor's text and caret, the Name Box, the completion list and its options, and the
     argument's hint. It also covers the positions grid: whether it is pointed at, its own Focus and
     Selection, its outlines and dashes with the rows and columns each covers, its scroller and client
     area, and the page's line saying why nothing was written.
   - From the page's own picture: each outline's line and fill, and the runs of the dashes along each
     side.
   - **Read-only fields were added to the probe during the run.** The Cell Editor's own `scrollLeft`,
     `scrollWidth` and `clientWidth` were added once the narrowness below had been seen in the first
     pictures. Records begun from 15:48:58 on carry them: `extra-sheet-server-150-*`,
     `sheet-server-150-msedge`, every 150 ms `/pointing` and narrow record (their additions too),
     and every `extra-a-*` record. The probe does nothing else differently.
6. **A trial** (15:41:48, b1 and b3 on WebAssembly under Chrome) tried the probe out. It is not
   recorded, and it changed nothing.

## Part A — Excel

`excel/ask-excel.ps1`, 15:31:54–15:36:19. An Excel of the script's own for every case, started with
`New-Object` and checked to be new. Every Formula was typed with real keys, and every click was the real
mouse. COM only set the case up, and read D10, the active workbook and the active cell after Escape.
Each state was read 600 ms after its last input: a picture, 300 ms, another picture, then UI Automation.

- **Groups 1 and 2**: Book1 alone, maximised, the Table `Positions` in A1:B4, typed into D10.
- **Group 3**: Book2 with the Table `Trades` beside it (View › Arrange All › Vertical, Book1 on the
  left), typed into Book1's D10.

**This time UI Automation read the list's rows** (`DataItem`, each listed twice in the tree, once per
level), which Part B of the ninth run could not. The pictures agree with every list below. The tips'
texts are read from the pictures: UI Automation gives the `XLToolTip` windows no name.

### Group 1 — what text at a value-list argument lists (Q50)

The edit was in Enter mode in every case. "The argument tip" is the ScreenTip under the cell
(`XLOOKUP(lookup_value, …)`), with the bold argument named.

| # | Typed into D10 | List (in order; selected first) | Tips |
|---|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,A1` | **all five values**: `0 - Exact match` (selected), `-1 - Exact match or next smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`, `3 - Regex match` | the argument tip, `[match_mode]` bold; beside the list "Searches for an Exact match, if not found return #N/A". `A1` is coloured as a Reference (purple) |
| 2 | `…,,1+` | **all five**, `0 - Exact match` selected | the same two tips (the list and its tip 11 px further right) |
| 3 | `…,,X` | **all five**, `0 - Exact match` selected | the same |
| 4 | `…,,AV` | **all five**, `0 - Exact match` selected; no function names | the same |
| 5 | `…,,Positions` | **all five**, `0 - Exact match` selected; no name of a Table | the same. `Positions` is coloured as a Reference (purple), and the Table is outlined in the sheet |
| 6 | `…,,"` | **all five**, `0 - Exact match` selected | the same |
| 7 | `…,,0,A` | **the four values of `search_mode`**: `1 - Search first-to-last` (selected), `-1 - Search last-to-first`, `2 - Binary search (sorted ascending order)`, `-2 - Binary search (sorted descending order)` | the argument tip, `[search_mode]` bold; beside the list "Perform a search starting at the first item" |
| 8 | `…,,0,5` | **no list** | the argument tip only, `[search_mode]` bold |
| 9 | `=SUM(A` | **the functions that begin with A**, `ABS` selected: the window shows `ABS` to `ARABIC` (12 rows) with a scroll bar; UI Automation lists 24 rows, `ABS` to `AVERAGEIFS` | `SUM(number1, [number2], ...)`, `number1` bold; beside the list "Returns the absolute value of a number, a number without its sign" |

After Escape, D10 held nothing in every case.

### Group 2 — the caret inside a value (Q49)

Each Formula was typed whole (Enter mode, no list, the argument tip `[match_mode]` bold), then F2 (Edit
mode, the caret still at the end), then the Left keys.

| # | Keys | After the Left keys | Tab |
|---|---|---|---|
| 10 | `=XLOOKUP(1,A2:A4,B2:B4,,-1)`, F2, ←← | Edit; the caret at 25, between `-` and `1`; **no list**; the argument tip, `[match_mode]` bold | **entered the Formula**: Ready, the active cell E10, D10 `=XLOOKUP(1,A2:A4,B2:B4,,-1)` |
| 11 | `…,,10)`, F2, ←← | Edit; the caret at 25, between `1` and `0`; **no list**; the same tip | **entered** `=XLOOKUP(1,A2:A4,B2:B4,,10)`; E10 active |
| 11a | `…,,  )` (two spaces), F2, ←←← | Edit; the caret at 24, straight after `,,`, before the two spaces; **no list**; the same tip | **entered** `=XLOOKUP(1,A2:A4,B2:B4,,  )`, the spaces kept; E10 active; D10 shows `#N/A` |

### Group 3 — the arrow keys straight after a column heading in another workbook (Q52)

Both workbooks showed Enter mode after the first click, and Point mode from the heading (or header
cell) click on. While Excel pointed into Book2, the Formula showed in both Formula Bars. UI Automation
read Book2's bar. Book1's bar and Book1's Name Box gave UI Automation no text, so they are read from
the pictures (`excel/shots/<case>-<state>-book1-formula-bar.png`, `…-name-box.png`).

- **The first click on Book2** (its D10) wrote nothing. The text stayed `=`, and Book1's Name Box
  showed `A1`, Book2's active cell.
- **The dashes** are Excel's green moving outline (`#217346`). Where they lay is read from the
  pictures, and from the pixels that changed on each cell's edges.
- **No popup** of Excel's showed in any state of this group: no list, and no tip.

| # | Keys and clicks | Written after each (Book2's bar; Book1's bar the same) | The dashes, in Book2 | Book1's Name Box |
|---|---|---|---|---|
| 12 | heading of column B | `=[Book2]Sheet1!$B:$B` | down the whole of column B | B1 |
| | then → | **`=[Book2]Sheet1!$C$1`** | C1 alone | C1 |
| 13 | heading of column B, then ← | **`=[Book2]Sheet1!Trades[[#Headers],[Id]]`** | A1 alone (the header cell `Id`) | A1 |
| 14 | heading of column B, then ↑ | **`=[Book2]Sheet1!Trades[[#Headers],[PV]]`** | B1 alone (the header cell `PV`) | B1 |
| 15 | heading of column B, then ↓ | **`=[Book2]Sheet1!$B$2`** | B2 | B2 |
| | then ↓ | `=[Book2]Sheet1!$B$3` | B3 | B3 |
| 16 | the header cell `PV` (B1) | `=[Book2]Sheet1!Trades[[#Headers],[PV]]` | B1 | B1 |
| | then → | **`=[Book2]Sheet1!$C$1`** | C1 | C1 |
| | then ↓ | `=[Book2]Sheet1!$C$2` | C2 | C2 |

So from a whole column pointed at by its heading, each arrow moved from the column's first row, row 1:

- → went to the next column's row 1, as a cell (`$C$1`);
- ← went to the previous column's row 1, which is the Table's header cell, written as such;
- ↑ stayed on row 1 and pointed at that one cell;
- ↓ went to row 2.

None of them pointed at a column. After Escape, D10 held nothing, and Book1 and D10 were active.

## Part B — ExSheet beside the decisions

**Every state read the same in all six configurations**: 70 of 70, the additions included
(`summary.txt`). That covers the DOM and the pixels alike, once a device pixel of rounding between the
browsers is taken out. The tables give each state as WebAssembly under Chrome read it.

- "Pointed at" is `ex-pointed-at` on the positions grid.
- "Dashes" are `.ex-point-dashes`, `dashed 2px #000000`.
- The column outlines are the Reference Outlines in their Reference's colour: `#326ac7` first,
  `#c0353e` second.
- **DOM focus stayed in the Sheet throughout.** On every page, the only `focusin` was the Cell
  Editor's when `=` opened the edit, and the Sheet's root after a commit or Escape. The positions grid
  never had a Focus or a Selection of its own.

### `/sheet`: the value list (Q49, Q51, Q53)

| # | Keys | Read | Reading | Matches |
|---|---|---|---|---|
| b1 | `=XLOOKUP(1,A2:A4,B2:B4,,1)`, F2, ←←, Tab | After ←←: the caret at 24, between `,,` and `1`; **no list**, the argument's hint alone. Tab: **committed**, the Focus on E10, D10 shows `12` | no list after ←←; Tab commits and moves to E10 (Q49) | yes |
| b2 | `…,,-1)`, F2, **←** (as the procedure writes it), Tab | After ←: the caret at **26, between `1` and `)`**, after the whole value; **the list shows `-1 - Exact match or next smaller item` alone, selected**, with the hint. Tab: **nothing committed**: `…,,-1)` stands, the caret at 26, the list closed, the hint shown, the edit open in D10 | no list; Tab commits (Q49, the caret inside a value) | **no** (see below) |
| b3 | `…,,`, Home | Before: the five values, `0 - Exact match` selected. Home: **the list closes; `…,,A10` is written**, the caret at 27; `A10` in the third Reference's colour on the grey (`#c6c6c6`), dashed in the Sheet; the Name Box `A10` | the list closes; A10 is pointed at and written (Q51) | yes |
| b4 | `…,,`, End | **The list closes; nothing is written**; the caret stays at 24; the edit stays open; the hint goes too | the list closes; nothing is written, and the edit stays open (Q53) | yes |
| b4a | `=SUM(`, Home; Escape; `=SUM(`, End | Home: **`=SUM(A10`**, the caret at 8, `A10` on the grey and dashed, the Name Box `A10`. Escape: no edit, D10 empty. `=SUM(` again, End: **nothing written**, the caret at 5, the edit open, the hint `SUM(number1, [number2], ...)` still shown | `=SUM(A10`, A10 pointed at; then nothing written, the edit open (Q53) | yes |
| b5 | `…,,`, Shift+→ | **The list closes; `…,,D10:E10` is written**, the caret at 31, `D10:E10` on the grey and dashed over D10:E10; the Name Box `D10` | the list closes; `D10:E10` is pointed at and written (Q51) | yes |
| b6 | `=Posit`, Home | Before: the list of names, `Positions` alone, selected. Home: **the caret to 0**; the list closed; nothing written; the edit open | the list of names: the caret moves to 0; the edit stays open (Q51, unchanged) | yes |

**b2 and the procedure's keys.**
- `=XLOOKUP(1,A2:A4,B2:B4,,-1)` ends with `-1)`. From the end, one ← puts the caret between `1` and
  `)`, after the whole value, not inside it. There, by ticket 55's rule, "a value typed whole lists
  that value alone", and Tab accepted it. Tab wrote `-1` over `-1` and kept the edit open.
- Part A's case 10 reached the caret inside the value (between `-` and `1`) with ←←.
- **The same keys with ←←** (b2x, an addition) gave what the reading says: no list, the hint alone,
  and Tab committed to E10, where D10 shows `#N/A`.

### `/pointing`: the arrow keys after a header press (Q52)

| # | Keys | Read | Reading | Matches |
|---|---|---|---|---|
| b7 | `=`, a press on PV's header, ↓, ↓ | The press wrote `=Positions[PV]`: the dashes over PV's body (R-1 to R-10, the rows painted), PV outlined `#326ac7`. **↓: `=XLOOKUP("R-1", Positions[Id], Positions[PV])`**, the dashes on R-1's PV, whole on all four sides; Id outlined `#326ac7`, PV `#c0353e`. **↓: `"R-2"`**, the dashes on R-2's PV. The Name Box empty throughout | `=XLOOKUP("R-1", …)`, then `"R-2"`; the dashes on the cell (Q52) | yes |
| b8 | `=`, a press on PV's header, ← | **`=Positions[Id]`**, passing over Book; the dashes over Id's body, Id outlined | `=Positions[Id]`, passing over Book; the dashes over Id's body (Q52) | yes |
| b9 | `=`, a press on PV's header, ↑ | **Unchanged**: `=Positions[PV]`, the dashes over PV's body; the page's line empty | unchanged; nothing told (Q52, an edge) | yes |
| b10 | `=`, a drag down PV's data (R-1 to R-9), ↓ | Drag: **`=`**, nothing dashed, and "What the press wrote was taken back: the drag reached another cell. A Formula reads one row of 'Positions' by its key, and a range of cells cannot be written." **↓: `=C4`**, the Sheet's own Point from C3, the Name Box `C4`; the positions grid still pointed at, nothing dashed in it; the line still shows the drag's reason | `=`, the drag's reason told; ↓ points in the Sheet, at C4 | yes |

### `/pointing?narrow`: the column scrolled into view, and the gutters (ticket 58, DC-53)

The positions grid is 260 px wide. Its scroller's client area is 248 × 268 CSS px, with **a 12 px
Scrollbar Gutter on the right and one at the bottom**. The columns are 300 px together (Id 90, Book 90,
PV 120), so the grid scrolls 52 px across.

| # | Set-up and keys | Read | Reading | Matches |
|---|---|---|---|---|
| b10a | First column in view (`scrollLeft` 0, `scrollTop` 0); `=`, a press on Id's header | `=Positions[Id]`, the dashes over Id's body | — | — |
| | → | **`=Positions[PV]`; `scrollLeft` 0 → 52**, `scrollTop` 0 unchanged. PV's dashes lie inside the client area across: their left side 128 px in, **their right side on the client area's right edge, beside the vertical gutter** (0 px). In the pixels their top is whole, their sides run down the painted part, and the client area's bottom edge cuts them (no bottom side) | `=Positions[PV]`, the grid scrolled across so that PV's dashes lie whole in view, not scrolled down (ticket 58) | yes |
| | ← | **`=Positions[Id]`; `scrollLeft` 52 → 0**, `scrollTop` unchanged; Id's dashes from the client area's left edge, 158 px short of its right | `=Positions[Id]`, Id whole in view (ticket 58) | yes |
| b11 | Last row and column (`scrollTop` 880, `scrollLeft` 52); `=`, a press on R-40's PV | **`=XLOOKUP("R-40", Positions[Id], Positions[PV])`**. **R-40's dashes lie inside the client area**: their right side on its right edge, their bottom on its bottom edge (0 px each), all four sides whole in the pixels. **Both column outlines end on the client area's right and bottom edges, or short of them**: PV's right and bottom at 0 px, Id's bottom at 0 px. Their tops (under the header, 40 px above the client area's top) and Id's left side (52 px scrolled out) are cut by the Viewport's edges, not by a gutter | the dashes and both column outlines inside the client area, beside both gutters (DC-53) | yes, as ticket 57 reads "inside": no outline runs past the client area's right or bottom edge |

### Additions

- **b2x**: b2 with ←←, above.
- **b10ax**: b10a with the rows wheeled down first, so that "not scrolled down" is asked of a grid
  whose vertical offset is not 0.
  - With the grid wheeled down 5 turns (`scrollTop` 500, rows R-18 to R-27 painted), → gave
    `=Positions[PV]` with `scrollLeft` 52, and ← gave `=Positions[Id]` with `scrollLeft` 0.
  - **`scrollTop` stayed 500 throughout.**
- **Part A's groups 1 and 2 typed into ExSheet** on `/sheet` (`a1`–`a11a`), beside what Excel did:

| Part A # | Typed | Excel (Part A) | ExSheet |
|---|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,A1` | the five values, `0` selected | **no list** (the argument's hint alone) |
| 2 | `…,,1+` | the five values, `0` selected | **no list** |
| 3 | `…,,X` | the five values, `0` selected | **`XLOOKUP`**, a function, selected |
| 4 | `…,,AV` | the five values, `0` selected | **`AVERAGE`**, selected |
| 5 | `…,,Positions` | the five values, `0` selected | **`Positions`**, the table's name, selected |
| 6 | `…,,"` | the five values, `0` selected | **no list** |
| 7 | `…,,0,A` | the four values of `search_mode`, `1` selected | **`AVERAGE`**, selected |
| 8 | `…,,0,5` | no list | no list |
| 9 | `=SUM(A` | the functions that begin with A, `ABS` selected (24 rows) | `AVERAGE` alone, selected |
| 10 | `…,,-1)`, F2, ←← (between `-` and `1`), Tab | no list; Tab entered the Formula, E10 active | no list; Tab committed, E10; D10 `#N/A` |
| 11 | `…,,10)`, F2, ←← (between `1` and `0`), Tab | no list; Tab entered the Formula | no list; Tab committed, E10; D10 `#VALUE!` |
| 11a | `…,,  )`, F2, ←←← (before the two spaces), Tab | **no list**; Tab entered `…,,  )`, the spaces kept | **the five values, `0 - Exact match` selected**; Tab wrote `…,,0  )`, the caret at 25, **the edit still open** |

- **b3 and b5 again**, to read the Cell Editor's own scroll (below, "Seen").

### Console messages and the hosts

- **Every page**: no message of type error or warning, and no page error.
- **WebAssembly**: one info message per load, "Debugging hotkey: Shift+Alt+D (when application has
  focus)".
- **Server**: two info messages per load, SignalR's "Normalizing '_blazor'…" and "WebSocket
  connected…".
- **The Server host's log**: only `info:` lines. The circuit log that `EXGRID_HOST_LOG` pointed to was
  never created, so no circuit raised an unhandled exception.

## Where Excel's answers bear on ADR-0058

Part A's procedure gives no readings: it asks. Each item below is what Excel answered beside what
ADR-0058 or a ticket says about it, or about what ExSheet does. Nothing here is decided.

1. **Q50: every text this run typed at `match_mode` listed every value, `0 - Exact match` selected**
   (cases 1–6). At `search_mode`, a number that is no value listed nothing (case 8).
   - ADR-0058, "Readings taken while building ticket 44", second bullet: "Text that begins no value
     lists nothing (`4`, `A1`, `1+` at `match_mode`)". Its note says Excel agreed for `4` and listed
     every value for `A`, and that "the rest waits for Excel".
   - Excel listed every value after `A1`, `1+`, `X`, `AV`, `Positions` and `"`.
   - At `search_mode`, `A` listed its four values, `1 - Search first-to-last` selected (case 7). `5`
     listed nothing (case 8), as `4` did at `match_mode`.
   - **`1+` begins with `1`, a value, and Excel selected `0`, not `1`.**
   - Neither `AV` nor `Positions` listed a function or a name. After `=SUM(`, `A` listed the functions
     (case 9).
   - ADR-0058, "What Part B of the ninth Windows run settled", second bullet (Q50): "nothing changes
     until Excel is asked more". What ExSheet lists for the same texts is in Part B's additions,
     above.
2. **Q49: with the caret inside a value, Excel listed nothing, and Tab entered the Formula** (cases 10
   and 11: `,,-|1)` and `,,1|0)`). This agrees with what ADR-0058, "What Part B of the ninth Windows
   run settled", first bullet, built "until Excel is observed".
3. **With the caret straight after `,,` and before two spaces, Excel listed nothing, and Tab entered
   the Formula with the spaces kept** (case 11a). Ticket 55's Comments: "White space after the caret
   before `,` or `)` was already read as nothing of the argument, and still is; Excel was not asked
   about it." By that rule ExSheet lists there; what it did is in Part B's additions, above.
4. **Q52: from a whole column pointed at by its heading, Excel's ← and → pointed at a cell of row 1,
   not at a column** (cases 12, 13).
   - → gave `=[Book2]Sheet1!$C$1`, a cell outside the Table.
   - ← gave `=[Book2]Sheet1!Trades[[#Headers],[Id]]`, the header cell of the previous column.
   - ADR-0058, "What Part B of the ninth Windows run settled", fourth bullet (Q52), its sub-bullet "←
     and → point at the next column the table has, as a column", which says "Excel was not asked →
     straight from a heading; from its heading cell, which is a row of the Table, it would reach the
     next column's heading".
   - From the header cell `PV` (case 16), → gave `$C$1`, the cell to its right outside the Table, and
     ↓ then gave `$C$2`.
5. **Q52: ↑ from the column's heading pointed at the column's row 1, the header cell
   `Trades[[#Headers],[PV]]`** (case 14). The same bullet's sub-bullet: "↑ is at an edge: nothing
   moves and nothing is told, as at any edge."
6. **Q52: ↓ from the heading pointed at `$B$2`, the first row of data, and ↓ again at `$B$3`** (case
   15). This agrees with the sub-bullet "↓ points at the column's first row", and with the ninth run's
   y2.

## Where ExSheet and a reading differ

1. **b2, with the procedure's keys** (F2, then one ←), read differently from the reading "no list;
   Tab commits (Q49, the caret inside a value)".
   - What happened: the caret was after the whole value `-1`, `-1 - Exact match or next smaller item`
     was listed alone, and Tab accepted it and kept the edit open.
   - With ←← (b2x), the caret inside the value, ExSheet read as the reading says.
   - ADR-0058, "What Part B of the ninth Windows run settled", first bullet (Q49); ticket 55's first
     criterion: "a value typed whole lists that value alone".

**Every other case of Part B read as its reading**: b1, b3 to b11, in all six configurations.

**What Part A's additions found in ExSheet beside Excel** (none of them has a reading):

2. **Q50, at `match_mode`**:
   - `A1`, `1+` and `"` list nothing in ExSheet; Excel listed every value.
   - `X`, `AV` and `Positions` list a function or the table's name in ExSheet; Excel listed every
     value.
   - At `search_mode`, `A` lists `AVERAGE` in ExSheet; Excel listed the four values.
   - `5` lists nothing in both.
   - ADR-0058, "What Part B of the ninth Windows run settled", second bullet: "nothing changes until
     Excel is asked more".
3. **Spaces after the caret** (Part A's 11a). ExSheet listed the five values and Tab wrote `0` before
   the spaces, the edit open. Excel listed nothing, and Tab entered the Formula. Ticket 55's Comments
   say of white space after the caret before `,` or `)`: "Excel was not asked about it."
4. **`=SUM(A`**: ExSheet lists `AVERAGE` alone; Excel listed every function that begins with A. This
   is the comparison case the procedure gives, and nothing in ADR-0058 reads it.

## Seen, and not asked

- **After Point writes a Reference at the end of a long Formula, the Cell Editor does not scroll to
  it.** This was seen in b3 and b5 on `/sheet`, in all six configurations.
  - The Cell Editor is 99 CSS px wide. Before Home (or Shift+→), its own `scrollLeft` was 81.3, showing
    the end of `=XLOOKUP(1,A2:A4,B2:B4,,`.
  - After the write it stayed 81.3, while the text grew: `scrollWidth` 181 → 205 for `A10`, 181 → 231
    for `D10:E10`.
  - So the caret at the end, and most of the Reference written, lay 24 px and 50 px beyond the
    editor's right edge.
  - The picture shows `,A2:A4,B2:B4,,` in the cell and nothing after it. The Formula Bar showed the
    whole text.
  - After an arrow or a press writes a lookup (b7's ↓, b11), the editor's `scrollLeft` is 0 with the
    caret at the end (`scrollWidth` 297 or 304). The editor shows the start, `=XLOOKUP("R-…`.
  - The coloured layer followed the field (its spans lie where the field's text lies).
  - The editor's own scroll was read only in the records listed in Method 5.
- **The narrow view's column outlines, read in the pixels.** Where an outline's box runs past the
  Viewport (Id's left side scrolled out in b11), the outermost pixels of its box read the page's white.
  That is outside the grid, not a line drawn white.
- **Excel's Name Box while it points into another workbook** shows the pointed cell, in the edited
  workbook's window (B1, C1, A1, B2…). UI Automation reads that box as empty: its text is read from the
  pictures.

## The machine afterwards

- **Processes.**
  - No Excel, no browser of Playwright's, and no input helper is running.
  - The latency proxy (the run's `node`) was ended at 15:57, and both DemoHosts in WSL were ended.
  - The user's Chrome and the Stream Deck's `node` were left alone.
- **Excel's AutoRecover workbooks were not touched.** `Book1 (version 1).xlsb`, the unsaved workbook
  and `Excel15.xlb` in `%APPDATA%\Microsoft\Excel` hash as the backup taken before the first Excel
  started (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run13`). The Office Theme was only read.
- **The keyboard**: English (UK), 0x08090809, as found. Each Excel's layout was put back to what it
  had been, which was 0x08090809. The regional format is en-GB, unchanged.
