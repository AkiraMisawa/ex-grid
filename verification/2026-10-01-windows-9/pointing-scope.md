# Verification — 2026-10-01, Windows, ExSheet's Pointing Scope beside Excel (ninth run, Part B)

**Scope: Part B of [`verify-on-windows-9.md`](../../docs/specs/exsheet/verify-on-windows-9.md)**:
ExSheet's Pointing Scope
([ADR-0058](../../docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md)) on
`/sheet`, `/sheets` and `/pointing`, in Chrome and Edge, on both hosts, at 150%; and the two "Ask Excel
too" items, observed in Excel and then typed into ExSheet. Part A is
[`../2026-09-30-windows-excel-9/pointing.md`](../2026-09-30-windows-excel-9/pointing.md).

**Verified commit: `3622d2cdc0f3f0f75598963257e9df34d626bf99`**, the tip of
`claude/exsheet-pointing-scope` when this run began. Tickets 34–38 and 41 say `Status: done` there
(and 44). The results are committed on `claude/exsheet-windows-verify-9b`, branched from that commit.
Nothing here changes an ADR, `CONTEXT.md` or the Definition of Done.

Files beside this one:

- `pointing-scope-probe.mjs`: the probe. Playwright opens a page, reads the DOM and takes the page's
  pictures. It sends no input: every key, press, drag and turn of the wheel goes through
  `input-server.ps1`. Its one addition to the page is a `focusin` listener of its own, which notes
  where DOM focus went between two readings.
- `input-server.ps1`: real OS input through `SendInput` and `mouse_event`. It is the eighth run's
  Part B helper, with a Shift+press, a drag, Ctrl and Shift together, a click held among keys in one
  `SendInput` call, the wheel, the cursor's handle, and a high-contrast switch that keeps the flags'
  other bits.
- `theme-state.ps1`: the display settings high contrast touches, as text (the eighth run's).
- `summarise.py`: one line per page, case and state; then whether every configuration read the same.
  `summary.txt` is its output.
- `records/`: one file per page and configuration, `<page>-<host>-<browser>.json`; `fast-*.json`, DC-54's
  check; `hc-*.json`, high contrast; `extra-g2w-*.json`, an addition; `first-pass-150/`, the first pass
  behind the 150 ms proxy (Method 6).
- `shots/`: the page's own picture of the Sheet and the grids beside it, at 600 ms, for every state of
  WebAssembly under Chrome (`<page>-<case>-<state>-wasm-chrome.png`), and of the high-contrast runs. The
  other configurations' pictures stay on the machine.
- `excel/`: the "Ask Excel too" items in Excel. `ask-excel.ps1` is the tenth run's `excel-only.ps1`
  with the ninth run's Book2 beside Book1; `ask-excel.jsonl` holds one line per case and pass;
  `excel/shots/` the pictures, with the account's initials blanked.

## Environment

- **The machine of the earlier runs**: Windows 11, one display, 3840×2160 at **150%** (144 dpi;
  `devicePixelRatio` 1.5 on every page). Light mode (`AppsUseLightTheme` 1, `SystemUsesLightTheme` 1),
  high contrast off except where stated, regional format en-GB.
- **Excel**: Microsoft 365, **Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, 64-bit**,
  the eighth to tenth runs' build. Office Theme "Use system setting" (File › Account; `UI Theme` 6).
  Edit directly in cell, Formula AutoComplete and Function ScreenTips on; "Use table names in formulas"
  on.
- **Chrome 153.0.8010.54 and Edge 154.0.4258.37**, as installed, headed, the window 1600×1050 DIP at
  the display's own scale (`viewport: null`; the page 1586×956 CSS px).
  - Driven by Playwright 1.62.1 under the portable Node v24.14.1, from a copy of `tests/ExGrid.Browser`
    (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-01-9b`) with `npm ci` run afresh in it.
  - A Chrome of the user's was open throughout. Playwright's Chrome is a process of its own, with its
    own profile.
- **Both DemoHosts ran in WSL from the verified commit's build** (`dotnet build ExGrid.slnx`: 0
  warnings, 0 errors, 00:35): WebAssembly on `localhost:5299`, Server on `localhost:6298`.
- **The 150 ms configuration** reached the Server host through `tests/ExGrid.Browser/latency-proxy.mjs`,
  run on Windows (5298 → 6298, control on 7298). The probe set `rtt=150` before its first key and
  `rtt=0` after its last; the proxy answered `rtt=150`.
- **The keyboard.**
  - Every browser window's layout was English (UK), 0x08090809, before and after each run. The probe's
    `english` request found it so and changed nothing. `VK_IME_OFF` was sent once per run.
  - Each Excel of `excel/` started with 0x08090809 too. The script posted it anyway, and restored
    what it found.
  - **Excel's keyboard check (case `0`) read one character back in lower case**: `+XLOOKUP` typed,
    `+xLOOKUP` in the Formula Bar and in the cell. The check compared without regard to case, and
    recorded `same: true`. Every other text of `excel/` reads in the Formula Bar as typed (the
    `.jsonl`), and so does every text of the browser runs (the field values in the records). The
    ninth and tenth runs' checks read back exactly. The check now compares case and all; run again at
    01:19 (pass `b`), it read back exactly.

## Method

1. **Excel** (`excel/ask-excel.ps1`), 00:49:18–00:52:09: an Excel of the script's own for every case,
   started with `New-Object` and checked to be new. Every Formula typed with real keys, every click and
   drag the real mouse; COM only set the case up. Each state: 600 ms, a picture, 300 ms, another, then
   UI Automation in each workbook's window (the Formula Bar's text and selection, the Name Box, the
   status bar's panes), the window in front, the focused element, and every popup with its UI
   Automation tree. Escape until Ready; then the active workbook and cell and D10 read through COM.
   - The three cases with Book2 (`y2`–`y4`) left their Excel running 10 s after `Quit`, as the ninth
     run's Book2 cases did, and the script ended each with `Stop-Process`. Each held only the script's
     two workbooks.
2. **Six browser configurations**, run one after another (00:53:36–01:08:06, then DC-54's check to
   01:11:02; the 150 ms ones again at 01:11:28–01:17:13, Method 6):
   - WebAssembly, Chrome and Edge;
   - Server, Chrome and Edge;
   - Server behind the 150 ms proxy, Chrome and Edge.

   In each, each page is one load: `/sheet` waited for until B12 shows `318.25` (the Linked Table's
   first push), `/pointing` until B1 shows `8200`, `/sheets` until its left Sheet shows `Left`, then
   1.5 s (its pushes follow the first render, and nothing on the page shows that they have landed).
   Screen pixels were calibrated against the page's own `mousemove` (one try each).
3. **Each case starts with no edit open and the Sheet's target cell selected and empty**: D10 on
   `/sheet`, D5 of the left Sheet on `/sheets`, C3 on `/pointing`. A case that entered something is
   cleared with Delete before the next. Keys go one every 30 ms. A press is the real mouse at the
   middle of the cell or the header. A drag moves in 20 steps of 15 ms. Positions cells are named by
   the grid's own A1 address: on `/sheet`, C3 is R-4471's PV.
4. **Each state is read 600 ms after its last input** (900 ms behind the 150 ms proxy), later only if
   the layer of the field with focus has not caught up with its value (none had to wait). Read:
   - **the DOM**: where DOM focus is, and where it went since the last reading; whether an edit is
     open; the Cell Editor's text and caret; the Name Box; the completion list and its selected option;
     whether the positions grid wears `ex-pointed-at`; its own Focus (`aria-activedescendant`) and
     whether its Selection is drawn; its Reference Outlines and dashes, each with its colours, the rows
     and columns it covers, and whether it lies inside the grid's scroller's client area; the page's
     line that says why nothing was written;
   - **the page's own picture** (`page.screenshot`; the screen copy returns nothing on this machine):
     each outline's line and fill; along each side of the dashes, the runs of pixels in the dashes'
     colour and how far along the side they reach; the pixels that changed in 300 ms.
5. **Trials (00:43–00:49)** tried the probe out and are not recorded. They changed two things before
   the recorded runs:
   - The dashes' pixels are read in the colour the DOM gives them. Read as the most frequent colour
     along each side, the short sides read the PV outline's red.
   - The scrollbar case (below). A drag meant for a resize grip pressed the header itself, because
     these pages have no grips, and left the positions grid with a Selection of its own. The grid is
     now scrolled with the wheel.
6. **Behind the 150 ms proxy, the first pass pressed within the round trip after `=`.** Where `=` and
   the press are one state (s3, t4, q1, y3, g2), the probe pressed straight after `=`, with no wait,
   before the grid had heard that the Sheet points. Each such press was an ordinary press: DOM focus
   went to the positions grid, its own Focus moved to the pressed cell, nothing was written, and the
   edit stood (`=`). The arrows after it moved the positions grid's own Focus (Ctrl+↓ to R-40), and two
   cases after them could not start (y4: the grid had scrolled away from the cell to press; g1: the
   Sheet was still editing). This is ADR-0058's first gap ("On a circuit", SH-35), in both browsers.
   Those records are kept in `records/first-pass-150/`.
   - **The probe then waited, between such keys and the press, until the positions grid wore
     `ex-pointed-at`** (156–176 ms behind the proxy), and the six 150 ms configurations ran again. That
     wait changes nothing on WebAssembly or at 0 ms, where the grid is pointed at before the press (the
     records of those runs show it), so they were not run again. The tables are the second pass's.

## Results

**91 of 92 states read the same in all six configurations** (`summary.txt`): the DOM and the pixels
alike, once a device pixel of rounding between the browsers is taken out (`summarise.py`). The one that
differs is g2's reading behind the 150 ms proxy, 900 ms after 32 ↓ (below).

The tables give each state as WebAssembly under Chrome read it.

- "Pointed at" is `ex-pointed-at` on the positions grid; "its own Focus" is the positions grid's
  `aria-activedescendant`.
- "Dashes" are `.ex-point-dashes`, `dashed 2px #000000` on these pages (the Focus outline's colour,
  `CanvasText` on `/sheet`, as the eighth run found).
- The column outlines are the Reference Outlines in the colour of the Reference they stand for
  (`#326ac7` first, `#c0353e` second).

### `=`, a press on a PV cell, `*2`, Enter

| Page | State | Text | DOM focus | Name Box | Positions grid |
|---|---|---|---|---|---|
| `/sheet` (s1) | `=` | `=` | the Cell Editor | D10 | pointed at |
| | press on R-4471's PV | `=XLOOKUP("R-4471", Positions[Id], Positions[PV])`, caret 48 | the Cell Editor | **empty** | pointed at; dashes on R-4471's PV; Id outlined `#326ac7`, PV `#c0353e`; no Focus or Selection of its own |
| | `*2` | `…[PV])*2` | the Cell Editor | D10 | not pointed at; the dashes gone; both outlines stay |
| | Enter | D10 shows **636.5** | the Sheet's root | D11 | nothing drawn |
| `/sheets` (t1), left Sheet D5 | the same keys, a press on the left grid's R-4471 PV | the same texts | the left Cell Editor, then the left root | empty after the press | as on `/sheet`; the right grid untouched |
| | Enter | D5 shows **636.5** | | D6 | |

**DOM focus never went to a positions grid**: the only `focusin`s the probe's listener heard were the
Cell Editor's when `=` opened the edit, and the Sheet's root after Enter.

### `=SUM(`, a press on the PV header, `)`, Enter

| Page | State | Text | Name Box | Positions grid |
|---|---|---|---|---|
| `/sheet` (s2) | `=SUM(` | `=SUM(` (the argument hint `SUM(number1, [number2], ...)`) | D10 | pointed at |
| | press on PV's header | `=SUM(Positions[PV]`, `Positions[PV]` on the grey (`#c6c6c6`, its text `#1b3a6d`) | empty | dashes over PV's body (all five rows), PV outlined `#326ac7`; no sort |
| | `)` | `=SUM(Positions[PV])` | D10 | not pointed at; dashes gone; the outline stays |
| | Enter | D10 shows **1189.4** | D11 | nothing |
| `/sheets` (t3) | the same, the left grid's header | the same | | the same |
| | Enter | D5 shows **1665.65** (1250 + 318.25 + 97.4) | D6 | |

### The arrow keys after a press

| Page | Keys | Text after each | Dashes after each | Name Box |
|---|---|---|---|---|
| `/sheet` (s3) | `=`, press R-4471's PV; ↓; →; ←; Shift+↓ | `XLOOKUP("R-4471", …[PV])`; **↓ `"R-5093"`, PV**; **→ unchanged** (PV is the last column); **← `Positions[Book]`** (Book is a column of `/sheet`'s table); **Shift+↓ unchanged, and the page says "Nothing was written: Shift+arrow points at more than one cell. …"** | R-4471 PV; R-5093 PV; R-5093 PV; R-5093 Book; R-5093 Book | empty throughout |
| `/sheets` (t4) | `=`, press R-1102's PV; the same keys | ↓ `"R-4471"`; → unchanged; ← `[Book]`; Shift+↓ refused, "Left: Nothing was written: Shift+arrow …" | follow the text | empty |
| `/pointing` (q1) | `=`, press R-1's PV; the same keys | ↓ `"R-2"`; → unchanged; **← `Positions[Id]`, passing over Book** (the grid's own column); Shift+↓ refused | R-1 PV; R-2 PV; R-2 PV; R-2 Id; R-2 Id | empty |

The column outlines follow the text: on `/sheet` after ←, Id and Book; on `/pointing`, Id alone once
the text reads `Positions[Id]` twice.

### Refusals

| Page | Keys | Text | The page's line |
|---|---|---|---|
| `/sheet` (s4a) | `=`, a Shift+press on R-4471's PV | `=` | "Nothing was written: more than one cell was pressed. A Formula reads one row of 'Positions' by its key, and a range of cells cannot be written." |
| `/sheet` (s4b) | `=`, a drag from R-1102's PV to R-4471's | `=` | "What the press wrote was taken back: the drag reached another cell. A Formula reads one row of 'Positions' by its key, and a range of cells cannot be written." |
| `/pointing` (q2) | `=`, a press on R-1's Book | `=` | "Nothing was written: the column 'Book' is not a column of the Linked Table 'Positions'." |

`/sheet`'s table has every column its grid shows (Id, Book, PV), so "a column the table does not have"
was pressed on `/pointing`. No dashes or outlines stay after any refusal, and the positions grid keeps
no Focus or Selection.

### The completion cases of `verify-on-windows-10.md`, group 1, on `/sheet`

Excel: the tenth run (`../2026-09-30-windows-excel-10/excel-only.md`, group 1).

| # | Keys | Excel (tenth run) | ExSheet |
|---|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,0` | `0 - Exact match` alone, selected | **`0 - Exact match` alone, selected** |
| 2 | `…,,-` | all five, `0` selected | **all five, `0 - Exact match` selected** |
| 3 | `…,,`, ↓, Tab | ↓ selects `-1`; Tab writes `-1`; no list | **the same**: `…,,-1`, caret 26, no list (the argument hint shows) |
| 4 | `=SUM(Positions[`, ↓ until PV, Tab | `@ - This Row`, `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals`; Tab gives `=SUM(Positions[PV`; the list closes | `Id`, `Book`, `PV` (the columns only, as decided), ↓↓ to PV; **Tab gives `=SUM(Positions[PV`; the list closes** |
| 5 | `=Posit`, Tab | `=Positions`; the list closes | **`Positions` alone; Tab gives `=Positions`; the list closes** |
| 6 | `…,,`, → | points: `E10`, shown selected; the list closes | **points: `…,,E10`, `E10` on the grey; the list closes**; the Name Box shows E10 |

### Ask Excel too (1): the readings taken while building ticket 44

ADR-0058, "Readings taken while building ticket 44". Excel: `excel/`, cases `x1`–`x6`, 00:49:33–00:50:41.
The Table `Positions` of `verify-on-windows-10.md` in A1:B4 of Book1, maximised; typed into D10.
ExSheet: the same keys typed into D10 on `/sheet` (cases `x1`–`x6` of the probe). `/sheet`'s A2:A4
and B2:B4 hold other values than Excel's, which no list here depends on.

- **Excel's list rows were read from the pictures.** UI Automation found the list's window
  (`__XLACOOUTER`, at its place under the edit) but gave it no rows this time: a pane holding a
  `SysListView32` with no children. The tenth run read the same list's rows (`DataItem`) through UI
  Automation. What was different is not known. Each list window is pictured
  (`excel/shots/<case>-<state>-popup-<n>.png`).
- To move the caret back in `x1`, F2 was pressed first, in Excel and in ExSheet alike. In Excel's Enter
  mode, and in ExSheet's Overwrite, an arrow points or ends the edit; it does not move the caret.

| # | Keys | Asked | Reading (ADR-0058) | Excel | ExSheet on `/sheet` |
|---|---|---|---|---|---|
| x1 | `=XLOOKUP(1,A2:A4,B2:B4,,1)`, F2, ←← (the caret between `,,` and `1`); then Tab | Is a list shown? What does Tab write? | Every value listed; the one chosen replaces the whole of `1` | Typed: Enter mode, no list. F2: Edit. ←←: caret 24, **no list** (the argument ScreenTip only, with `[match_mode]` bold). **Tab entered the Formula** into D10 (`=XLOOKUP(1,A2:A4,B2:B4,,1)`) and the active cell moved to E10 | After ←←: **the five values listed, `0 - Exact match` selected**. **Tab wrote `=XLOOKUP(1,A2:A4,B2:B4,,0)`**, caret 25, the edit open |
| x2 | `=XLOOKUP(1,A2:A4,B2:B4,,4` | Is a list shown? | Nothing listed | **No list** (the argument ScreenTip only) | **No list** |
| x3 | `=XLOOKUP(1,A2:A4,B2:B4,,A` | Is a list shown? | Nothing listed (the ADR names `A1`) | **The five values of `match_mode`, `0 - Exact match` selected**, and beside the list a tip describing the selected value ("Searches for an Exact match, if not found return #N/A") | **A list of one function name, `AVERAGE`**, selected |
| x4 | `=XLOOKUP(1,A2:A4,B2:B4,,`, then Home | With the value list open, what does Home do? | The editor's: the caret to the start | **Home pointed**: `=XLOOKUP(1,A2:A4,B2:B4,,A10`, `A10` selected (24–27), Point mode; the list closed; a value tip `0` | **The caret went to 0**; the list closed; nothing written |
| x5 | as x4, then End | What does End do? | The editor's: the caret to the end | **Point mode with End Mode on** (the status bar shows "End Mode"); nothing written; the list closed | **Nothing changed**: the caret stayed at 24, the end, and **the list stayed open** |
| x6 | as x4, then Shift+→ | What does Shift+→ do? | The editor's: text selected | **Shift+→ pointed**: `=XLOOKUP(1,A2:A4,B2:B4,,D10:E10`, `D10:E10` selected (24–31), Point mode; the list closed; a value tip `{0,0}` | **Nothing changed**: the caret at 24, nothing selected, **the list stayed open** |

### Ask Excel too (2): what building ticket 41 left to Excel

ADR-0058, "Settled while building ticket 41". Excel: `excel/`, cases `y1`–`y4`, 00:50:45–00:51:58. Book2
with the Table `Trades` beside Book1, as in Part A (View › Arrange All › Vertical, Book1 on the left),
typed into Book1's D10.

- **The first click on Book2 lands on its D10**, away from the Table. Excel points into another workbook
  only from its second click (Part A), and that click wrote nothing here either (`=` stayed, Enter
  mode). The drag or the heading click after it is the one that points.
- The outline in Book2 is the green moving dashes (`#217346`). Where it lay is read from the pictures
  and from the pixels that changed on each cell's edges.
- While Excel points into Book2, the Formula is shown in Book2's Formula Bar as well as Book1's.

| # | Keys and clicks | Excel: written, and the outline |
|---|---|---|
| y1 | `=`, a click on Book2 (D10), a drag from Book2's B2 to B4 (all of `Trades[PV]`'s data), ↓, → | Drag: **`=[Book2]Sheet1!Trades[PV]`**, the dashes around B2:B4. **↓: `=[Book2]Sheet1!$B$3`**, the dashes on B3 alone. **→: `=[Book2]Sheet1!$C$3`**, the dashes on C3. Point mode throughout |
| y2 | `=`, a click on Book2 (D10), a click on Book2's column B heading, ↓, → | Heading: **`=[Book2]Sheet1!$B:$B`**, the dashes down the whole column. **↓: `=[Book2]Sheet1!$B$2`**, the dashes on B2. **→: `=[Book2]Sheet1!$C$2`**, on C2 |
| y3 | `=`, a click on Book2's B3 twice (case 6x), Ctrl+↓ | Second click: `=[Book2]Sheet1!$B$3`. **Ctrl+↓: `=[Book2]Sheet1!$B$4`**, the dashes on B4, the last row of the Table's data |
| y4 | `=`, a click on Book2's B3 twice, Ctrl+Shift+↓ | **Ctrl+Shift+↓: `=[Book2]Sheet1!$B$3:$B$4`**, the dashes around B3:B4 |

So, from a whole column pointed at in another workbook, ↓ pointed at a single cell: B3 after the drag
(which began on B2), and B2 after the heading. → then pointed one column to the right of that cell.

ExSheet, the same keys on `/pointing` (cases `y1`–`y4` of the probe). ExSheet points from the first
press (decided, ADR-0058, "What the ninth Windows run settled"), so Excel's first click on Book2, which
writes nothing, has no counterpart. Positions R-1 to R-40 are the grid's rows; Excel's B3 is R-2's PV.

| # | Keys | ExSheet: written, and the dashes |
|---|---|---|
| y1 | `=`, a drag down PV's painted data (R-1 to R-9), ↓, → | Drag: **`=`**, nothing dashed; "What the press wrote was taken back: the drag reached another cell. …". **↓: `=C4`**: the Sheet's own Point, from C3, the cell being edited; nothing in the positions grid. **→: `=D4`**. The Name Box names C4, then D4. The line still shows the drag's reason |
| y2 | `=`, a press on PV's header, ↓, → | Header: **`=Positions[PV]`**, dashes over PV's body. **↓: unchanged**, and "Nothing was written: the arrow keys move from a cell, and a column's header of 'Positions' was pressed. Press a cell to point by keys." **→: the same.** The dashes stay on the column |
| y3 | `=`, a press on R-2's PV twice, Ctrl+↓ | Each press: `=XLOOKUP("R-2", Positions[Id], Positions[PV])`, dashes on R-2's PV. **Ctrl+↓: unchanged**, and "Nothing was written: Ctrl+arrow goes to the edge of the data, and the grid holds only the rows near those it shows, so where 'Positions' ends is not known. Use the arrow alone." |
| y4 | as y3, Ctrl+Shift+↓ | **Unchanged**, and the same line as Ctrl+↓ |

### The pointer over the positions grid (`/sheet`, p1)

The real pointer was moved over R-4471's PV and over PV's header (no press). The computed `cursor` of
the element under it, and the cursor Windows shows (`GetCursorInfo`):

| When | Over the cell | Over the header |
|---|---|---|
| No edit open | `auto`; the system arrow | `auto`; the system arrow |
| After `=` (pointing) | **`cell`**; a cursor that is none of the system's (Chrome's own) | **`cell`**; the same |
| After Escape | `auto`; the system arrow | — |

### The positions grid with its own scrollbars (`/pointing`, g1 and g2)

**The procedure's narrowing could not be done as written.** The positions grids of `/sheet` and
`/pointing` are 330 px wide by their `ViewportWidth`, and neither page wires `OnColumnWidthChanged`, so
their headers have no resize grips. Neither the grid nor a column can be resized without changing the
page. `/sheet`'s grid (5 rows in 420 px) shows no scrollbar.

What there is: `/pointing`'s grid has its own vertical scrollbar (40 rows in 280 px). Its scroller's
client area is 318 × 280 CSS px: a 12 px Scrollbar Gutter on the right, none at the bottom. PV, the last
column, ends at 300 px, **18 px short of the gutter**, so nothing of it lies against the gutter.

| Case | Set-up and keys | What was read |
|---|---|---|
| g1 | The wheel turned 15 times down over the grid (`scrollTop` 868, the last rows R-31 to R-40 painted, R-40 flush with the client area's bottom); `=`, a press on R-40's PV; ↑; ↓ | `XLOOKUP("R-40", …)`, then `"R-39"`, then `"R-40"`. **R-40's dashes lie inside the client area, and all four sides are whole** in the pixels (20, 4, 20 and 4 runs, each from end to end). The column outlines run from under the header to R-40's bottom; their bottoms are whole |
| g1 | Escape; `=SUM(`, a press on PV's header | `=SUM(Positions[PV]`. The column's dashes run from under the header (their top side hidden there) to R-40's bottom, which is whole |
| g2 | The wheel turned back to the top; `=`, a press on R-8's PV; ↓ 32 times | `XLOOKUP("R-40", …)`. **The grid scrolled to keep the pointed cell in view** (`scrollTop` 0 → 868); R-40's dashes whole, inside the client area. **Behind the 150 ms proxy, 900 ms after the last ↓, the text was at `"R-18"`** and the dashes were still moving (224 px changed in 300 ms), in both browsers. Read again 6 s later (`g2w`, an addition, `records/extra-g2w-*`), it was at `"R-40"`: each ↓ is a round trip, and none was lost |

Before the grid is scrolled to its end, the column outlines and the column's dashes run down through
the tenth, partly painted row and are cut by the client area's bottom edge (their bottom side not
drawn). That edge is the Viewport's, not a gutter.

### Another Scope's grid (`/sheets`, t2)

The left Sheet's D5: `=`, then a press on the right grid's R-4471 PV:

- **An ordinary press**: DOM focus went to the right grid's root, its Focus moved to that cell
  (`aria-activedescendant` `…-r1c2`) and its Selection is drawn. Nothing was written.
- **The edit stands**: the left Cell Editor still holds `=`. The left positions grid is no longer
  pointed at while the keyboard is in the right grid.
- A press back inside the left Cell Editor brings the keyboard back, and the left grid is pointed at
  again. The right grid keeps its Selection.

### DC-54: `=`, a press and `*` as fast as `by-hand.ps1` sends keys (Server, 150 ms)

`/sheet`, D10, the press on R-4471's PV. `by-hand.ps1` sends a string with one `SendKeys.SendWait`, so,
as the eighth run did, the events went in **one `SendInput` call**: the key events and the left button's
press and release, with the pointer put on the cell first (3–12 ms per call). The text was read 3 s
later, when every round trip had been answered. Ten times each, in each browser:

| How it was sent | Chrome | Edge |
|---|---|---|
| **`=`, the press and `*` in one call** (the procedure's words) | **`=` 10 of 10.** The press was an ordinary press: DOM focus in the positions grid, its own Focus on R-4471's PV; the `*` went to the positions grid; the edit stood | **`=` 10 of 10**, the same |
| `=` typed; the positions grid waited for until it wore `ex-pointed-at` (156–176 ms); then the press and `*` in one call (as DC-54's layer-3 test does) | **`=XLOOKUP("R-4471", Positions[Id], Positions[PV])*` 10 of 10**; DOM focus in the Cell Editor | **10 of 10**, the same |

`records/fast-literal-*.json` and `records/fast-pointed-*.json`.

### Windows' high contrast on (`/sheet`, WebAssembly)

High contrast was switched on through `SystemParametersInfo(SPI_SETHIGHCONTRAST)` with only its on bit
changed (flags 0x7e → 0x7f); Windows applied "High Contrast Black". The probe ran with no emulation
(`forcedColors: null`, `colorScheme: null`), and the page reported `forced-colors: active` and
`prefers-color-scheme: dark`. 01:18:29–01:19:14, Chrome and Edge alike:

- **h1, `=` and a press on R-4471's PV**: the Id and PV outlines are **both white** (`#ffffff`, 2 px),
  over the page's `#202020`; their fill is the ground. **The pressed cell's dashes are white too**
  (`dashed 2px #ffffff`), and show as a dashed line inside PV's solid one: 20 runs along the top and the
  bottom.
  Which column is read is still shown; which Reference reads which is not. The spans' `color` computes
  white; their `-webkit-text-fill-color` still computes the Reference's colour.
- **h2, `=SUM(` and a press on PV's header**: PV's outline white, and the column's dashes white inside
  it. `Positions[PV]`'s grey ground is gone (`#202020`).
- `shots/sheet-h1-pressed-wasm-hc-*.png`, `shots/sheet-h2-header-wasm-hc-*.png`;
  `records/hc-wasm-*.json`.

### Console messages

- **Every page**: no message of type error or warning, and no page error.
- **WebAssembly**: one info message per load, "Debugging hotkey: Shift+Alt+D (when application has
  focus)".
- **Server**: two info messages per load, SignalR's "Normalizing '_blazor'…" and "WebSocket
  connected…".
- **The Server host's log**: only `info:` lines. The circuit log `EXGRID_HOST_LOG` pointed to was never
  created, so no circuit raised an unhandled exception.

## Where Excel and a reading differ

Each is a difference between what Excel did and a reading ADR-0058 took while ExSheet was built, or a
statement ADR-0058 makes about Excel. Nothing here is decided.

1. **With the caret before a value, Excel lists nothing** (x1). ADR-0058, "Readings taken while building
   ticket 44", first bullet: "With the caret before or inside a value (`,,|1)`, `,,-|1)`), every value is
   listed, and the one chosen replaces the whole of it." In Excel, Tab with no list open **entered the
   Formula** (`,,1)` kept) and moved to E10. ExSheet, as read, lists the five values and Tab writes
   `,,0)`, the edit open.
2. **`A` at `match_mode` lists every value, `0 - Exact match` selected** (x3), with a tip describing the
   selected value. Same paragraph, second bullet: "Text that begins no value lists nothing (`4`, `A1`,
   `1+` at `match_mode`)". `4` agrees (x2, no list). The procedure asks `,,A`; `A1` and `1+` were not
   typed. ExSheet showed a list of function names (`AVERAGE`), not the values.
3. **Home, End and Shift+→ with the value list open are not the editor's in Excel** (x4–x6). Same
   paragraph, third bullet: "`Home`, `End` and the Shift+arrows stay the editor's while any list is
   open". In Excel each closed the list and put the edit in Point: **Home pointed at A10**, **End turned
   on End Mode** and wrote nothing, **Shift+→ pointed at `D10:E10`**. ExSheet, as read: Home put the
   caret at 0 and closed the list; End and Shift+→ changed nothing and left the list open.

**What Excel answered where ADR-0058 waited for it** (ADR-0058, "Settled while building ticket 41",
first bullet: "Where ↓ would go from a column, and ← and → from one column to the next, is left until
Excel is observed pointing at a whole column of another workbook"), and its readings:

4. **From a whole column of another workbook, ↓ pointed at a single cell, and → one column right of
   it** (y1, y2): after the drag over `Trades[PV]`'s data (begun on B2), ↓ gave `$B$3` and → `$C$3`;
   after the column heading `$B:$B`, ↓ gave `$B$2` and → `$C$2`. The drag wrote
   `[Book2]Sheet1!Trades[PV]`, the heading `[Book2]Sheet1!$B:$B`. ExSheet refuses both arrows after a
   header press, as decided (Q48).
5. **Ctrl+↓ went to the edge of the data** (y3: `$B$4`), as ADR-0058, "The keyboard", says of Excel.
   **Ctrl+Shift+↓ pointed at the range to that edge** (y4: `$B$3:$B$4`). The paragraph's first reading
   says "Ctrl+Shift+arrow is refused as Ctrl+arrow is (the edge of the data), not as a range"; ExSheet
   refuses it with Ctrl+arrow's reason, as read.

Every other statement about Excel that this run met agreed: the first click on another workbook writes
nothing (ADR-0058, "What the ninth Windows run settled"); `4` lists nothing; and the completion cases
of the tenth run, typed into ExSheet, gave Excel's answers (above).

## Where ExSheet and ADR-0058 differ

**No reading of ExSheet contradicted ADR-0058's rules, or the criteria it names (DC-52 to DC-55, SH-32,
SH-34 to SH-36), in what was asked**, in any of the six configurations. Five things are listed because
the procedure, or ADR-0058, says something this run did not match, or does not say:

1. **DC-54, as the procedure words it, gave `=` 20 of 20** (Server, 150 ms): `=`, the press and `*` in
   one call. The press was an ordinary press, the `*` went to the positions grid, and the edit stood.
   The procedure: "The text must be `=XLOOKUP(…)*` every time (DC-54)". ADR-0058, "On a circuit", first
   bullet, and SH-35 ("On a circuit, a press within the round trip after `=` is an ordinary press, and
   the edit stands") describe what happened. With `=` typed first and the press made once the grid was
   pointed at, as DC-54's layer-3 test does, it was `=XLOOKUP(…)*` 20 of 20.
2. **The same first gap ran through the first 150 ms pass** (Method 6): a press straight after `=`
   was ordinary in s3, t4, q1, y3 and g2, and the arrow keys after it moved the positions grid's own
   Focus. Again ADR-0058, "On a circuit", and SH-35.
3. **After a drag took its press back, ↓ and → pointed inside the Sheet** (y1, `/pointing`): `=C4`, then
   `=D4`, from C3, the cell being edited; nothing in the positions grid; the line still gave the drag's
   reason. ADR-0058, "The keyboard": "After a press on a registered grid, the arrow keys point inside
   that grid"; "What is written": a drag "takes back what its press wrote … the dashes go". ADR-0058
   does not say which applies after a drag's press was taken back.
4. **The scrollbar item could not be run as written** (DC-53; the procedure's "the positions grid
   narrowed until it shows its own scrollbars"). The grids' width is the page's, and their headers
   have no resize grips. On `/pointing`'s own vertical scrollbar, the last column ends 18 px short of
   the gutter, and the last row's dashes and outlines were whole once scrolled to it.
5. **Behind 150 ms, 32 ↓ took more than 900 ms to land** (g2): the text was at `"R-18"` 900 ms after the
   last key, and at `"R-40"` 6 s later. Nothing was lost. ADR-0058 says the arrows rewrite the text a
   round trip after the key (SH-35); it gives no time.

## The machine afterwards

- **Processes**: no Excel, no browser of Playwright's, no input helper. The latency proxy (the run's
  `node`, started 00:35) was ended at 01:20; another `node` on the machine, not the run's, was left
  alone. Both DemoHosts in WSL were ended.
- **Excel's AutoRecover workbooks were not touched**: `Book1 (version 1).xlsb` and the unsaved workbook
  in `%APPDATA%\Microsoft\Excel` hash as the backup taken before the first Excel started
  (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run9b`). The Office Theme is as found.
- **High contrast** is off, its flags 0x7e as before. Turning it off left what the eighth run found:
  the `HighContrast` key's record of the last scheme (`High Contrast Scheme`, `Previous High Contrast
  Scheme MUI Value`, and the new `Previous High Contrast Scheme MUI Ptr` and `LastUpdatedThemeId`), and
  `MenuHilight` at `51 153 255`. Each was put back to the value read before (the two new values
  removed; `MenuHilight` `0 120 215` in the registry and live, `SetSysColors`). **`theme-state.ps1`
  then read identically to its reading before** (01:18), the scheme name that
  `SPI_GETHIGHCONTRAST` keeps in memory included ("High Contrast Black", there since the eighth run).
- **The keyboard**: English (UK), 0x08090809, as found. Excel's keyboard check, run again at 01:19
  (case `0`, pass `b`, comparing case too), read back exactly.
