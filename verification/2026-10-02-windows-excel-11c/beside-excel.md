# Windows, eleventh run, Part C: ExSheet beside Excel

[`docs/specs/exsheet/verify-on-windows-11.md`](../../docs/specs/exsheet/verify-on-windows-11.md), Part C.
ExSheet is put beside Part A's Excel screenshots
([`../2026-10-01-windows-excel-11/`](../2026-10-01-windows-excel-11/cell-format.md)), case by case, on
`/sheet?case=<name>`.

- **Verified commit:** `c09c7594b38e296b62cda5a3352d89e665046c88`, the tip of
  `claude/exsheet-windows-verify-11c` ("The eleventh run's Part C: ExSheet beside Excel, case by case").
  It is `claude/exsheet-cell-format` at `523c0b9` with Part C's procedure added. The results are
  committed on that branch, as asked.
- **When:** 2026-10-02, from 00:03 to 00:50 local time (BST). The recorded runs went from 00:32 to 00:50;
  before them, trials found faults in the probe (Method, below).
- **Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed.

## Summary

Of the 23 cases asked, **16 read the same as Excel**: 1, 2, 3b, 4, 5, 6, 7, 10, 12-1, 12-14, 13, 14, 16, 17,
18 (under en-US, the page's culture) and 25. Cases 22 and 24 differ only in what Format Cells offers,
and case 8 at 100% only in the row's height. The rest differ as follows.

### Every difference

**Marked deliberate** where ADR-0071 records it as such: "Where ExSheet stays unlike Excel" (one row
height; Ctrl+1 during an edit), and "What stays out" or "ExSheet decides what it offers" for what Format
Cells leaves out.

1. **Rows keep one height** (cases 8, 9, 12x). *Deliberate: ADR-0071, "Excel raises a row's height for a
   medium or a thick line".*
   - Every row stayed 28 CSS px, at 100% and at 150%, with every line drawn.
   - Excel raised rows 2 and 3 to 15 pt for a thick line (case 8). In case 9 it raised a row beside
     every Medium or Thick weight: medium, thick, double, the three medium dashes and slanted dash-dot.
2. **Ctrl+1 during an edit.** *Deliberate: ADR-0071.* No case of Part C opens an edit, so it was not
   seen.
3. **Case 12: Ctrl+Shift+= inserts no row; Chrome zoomed the page to 110%.**
   - Row 3 was selected with Part A's keys (A3, Shift+Space). Then Ctrl+Shift+= was sent as the
     characters on the UK layout (Ctrl with `+`, which is Shift+`=`).
   - ExSheet changed nothing: row 3 stayed empty and unfilled, and the Selection stayed `3:3`.
   - **The browser took the key as its own Ctrl++ and zoomed the page to 110%** (`devicePixelRatio` 1.5
     → 1.65). The zoom stayed for the site: the next case's page opened at 110%, and the probe set it back
     with Ctrl+0 (recorded in `records/wasm-chrome.json`, `zoomResets`).
   - Excel inserted a row (Part A, case 12).
   - ADR-0071 places ExSheet's insert in the Context Menu, as a Consumer command. ADR-0050 item 14 says a
     key no Consumer declares stays the browser's.
   - **Case 12x (an addition)** inserted the row from the Context Menu instead ("Insert rows above",
     right click on A3). There **ExSheet reads as Excel**:
     - the new B3 is yellow;
     - the thick line stays between B2 and B3 (`-2..0 000000` across B2's bottom);
     - B3 has no bottom line, and B2's thin top is not repeated;
     - the rows keep their height (item 1).
4. **Case 3c: the number of `#`.** At column A's width of 9.5, ExSheet shows `######` (six) where Excel
   showed `#########` (nine). Both are red (`#FF0000`).
5. **Case 9 at 100%: the long dashes are 9 px.**
   - In ExSheet, every dash family's long dash is **9 on, 3 off** at 100% (`devicePixelRatio` 1.5).
   - Part A's 100% table reads 8 for the dash-dot, dash-dot-dot and medium families. ADR-0071's reading
     and layer 3 (`line-pixels.mjs`: 8 below a scale of 1.5) take that 8.
   - The fourteenth run (`../2026-10-01-windows-excel-14/cell-format-14.md`, case 18) found Excel's every
     full dash 9 px at every zoom, at this display scale. Part A's reading began 8 px inside the cell
     and cut its first dash.
   - Slanted dash-dot: ExSheet's upper row reads 11, 1, 5, 1 and its lower row 10, 2, 4, 2. Part A's
     40 px reading gives the upper 11, 1, 5, 1 and the lower 9, 2, 4, 2, its first run starting 8 px in.
6. **Cases 8 and 9 at 150%: the lines follow the page's zoom.**
   - At 150%, ExSheet was read at the browser's zoom of 150%: `devicePixelRatio` 2.25, matching Excel
     at zoom 150% on this display.
   - **A gridline is 2 device px** (`#E1E1E1`, `#E0E0E0`); Excel's is 1 px.
   - **Thin covers one of the gridline's two pixels** and leaves the other grey (`-2 e0e0e0 | -1 000000`).
     Excel's thin covers its gridline.
   - **Thick is not centred**: it covers both of the gridline's pixels and one below (`-2..0`). Excel's
     is centred: one pixel above the gridline, the gridline, one below (`-4..-2`, gridline `-3`).
   - Medium covers the gridline's two pixels (Excel: the gridline and one above). Double is two
     1-px lines with a white pixel between, as in Excel.
   - **The dashes grow with the zoom**:

     | Style | ExSheet | Excel |
     |---|---|---|
     | Dashed | 4 or 3 on, 1 off | 3, 1 |
     | Dotted | 3 or 2 on, 2 off | 2, 2 |
     | Dash-dot | 10 or 11, 3 or 4, 3 or 4, 3 | 9, 3, 3, 3 |
     | Medium dashed | 10 or 11 on, 3 off | 9, 3 |
     | Hair | 2, 1, 1, 1, … (uneven) | 1, 1 |

   - Edge and the Server host read the same patterns. Edge rounds the cell's edge one device pixel
     lower, so its offsets are one less.
7. **Case 11: the Selection over the lines.**
   - **What reads as Excel:**
     - the outline lies over the outer lines;
     - the lines inside the range stay drawn, dark, over the shade;
     - the Focus cell B2 stays unshaded (white).
   - **The outline differs.** Excel's is 2 device px of `#217346`, on the gridline and one pixel outside
     it, with a white line inside it. ExSheet's is 3 device px (2 CSS px), on the gridline and two
     pixels outside, with no white line:
     - in the Ink, `#000000`, under the built-in Chrome;
     - in the palette's primary, `#594AE2`, under ExSheet.MudBlazor's (an addition).
   - **The shade differs.** Excel: `#C7C7C7`. ExSheet: `#DCE7F8` (built-in) and `#E1DEFA` (Mud).
   - **The inner lines are tinted by the shade**: `#0B1627` (built-in) and `#100D29` (Mud), where
     Excel's stay `#000000` above its shade.
8. **Case 22: Format Cells' tabs and edges.**
   - Five tabs: Number, Alignment, Font, Border, Fill. Excel has six, with Protection. *Recorded by
     ADR-0071 ("ExSheet decides what it offers": Protection is left out).*
   - The Border tab's edges are Top, Horizontal, Bottom, Left, Vertical and Right. Excel adds Diagonal
     Up and Diagonal Down. *Recorded by ADR-0071 ("What stays out": diagonal borders).*
   - Everything else matched (the table below).
9. **Case 24: the Font tab has no *Normal font* box.** Excel draws one in its mixed state for A1:A2.
   ExSheet's Font tab has no such box. Otherwise the parts that differ show as Excel shows them.

### Seen beside the readings

These were not asked for.

- **Format Cells on C2 shows C2's own left, not the line on that edge** (cases 7 and 7x).
  - B2's thick red right was drawn on the edge between B2 and C2.
  - Format Cells over C2 showed *Left* unpressed and drew no line on the preview's left.
  - The fourteenth run's case 13 found Excel's Border tab showing the neighbour's line there.
- **An edge button that already shows the chosen line takes it off.** In a trial, the case-7 page's
  B2 already held a thick red right. Choosing Thick and Red, then *Right*, set it to none
  (`aria-pressed` false). So case 7 opened Format Cells on B2 only to read it, then pressed Cancel. Case
  7x, an addition, set both edges through Format Cells on a Sheet with nothing set.
- **Escape with no edit open sends the keyboard from the Sheet to the page.** The keys that followed went
  nowhere (a trial; the fifteenth run saw the same).
- **Format Cells under the built-in Chrome scrolls inside the Sheet's box.** At this window size the
  palettes, *More Colours* and OK lay below the part that shows, and the wheel had to scroll to them.
- **ExSheet.MudBlazor's tab strip scrolls sideways.** When the dialog opened on Border (case 24), Font
  and Fill were not shown. The arrow keys from the tab shown reached them.
- **Gridlines.** At 100%, a vertical gridline is two device pixels, `#F0F0F0` then `#E0E0E0` (1 CSS px
  at a scale of 1.5); a horizontal one is one pixel, `#E0E0E0` or `#E1E1E1`. Excel draws both as 1 px of
  `#E0E0E0`.
- **The Row Headings' edge** is `#C6C6C6` (Excel: `#ABABAB`). A line on column A's left lies under it,
  as in Excel (case 12-14).
- **No error triangle on `#DIV/0!`** (case 3b). Excel draws its green one.
- **The cells are larger.** ExSheet's rows are 28 CSS px (42 device px) and its columns 99 CSS px
  (148.5 device px). Excel's rows were 29 device px and its columns 96. So the pictures beside each other
  are at different sizes; the offsets across an edge compare directly.
- **The dark scheme** (cases 1, 4 and 9, `&scheme=dark`). The Paper stays white, and the values, Fills,
  gridlines and lines read **pixel for pixel as in the light scheme**. Around the Sheet, the Headings and
  the page are dark (`4-set-wasm-chrome-dark.png`). This is SH-39.
- **The browsers and hosts agree.** At 100%, cases 8, 9, 11, 16, 17 and 18 read the same in Chrome and
  in Edge, on the WebAssembly host and on the Server host: every edge, every line's runs, every cell's
  text and style.
- **Console:** information messages only (`Debugging hotkey…` on WebAssembly; the circuit's
  `Normalizing '_blazor'…` and `WebSocket connected…` on the Server host).

## Files

| | |
|---|---|
| `beside-excel.md` | this report |
| `beside-excel-probe.mjs` | the probe: opens each case's page, presses Part A's keys and clicks with real input, and reads the page after each step |
| `input-server.ps1` | the probe's real OS input (`SendInput`): the fifteenth run's helper, with Ctrl and a character's key (`ctrlchar`), a drag with Ctrl held, a right click and the named keys Ctrl+B, Ctrl+1 and the like need |
| `records/<configuration>.json` | every state of every case: the steps, what the DOM held (each cell's text and computed style, the rows' heights, the Selection, Format Cells' tabs and controls), and the pixels (across each edge, along each line, each cell's text colours) |
| `shots/` | the page's picture of the Sheet in each state, A1 to about H16 with the Headings, and Format Cells when open (`<case>-<state>-<configuration>.png`) |
| `crops/` | the cells each case reads, cut from those pictures and enlarged two or four times, as Part A's crops are |
| `crops.py`, `png.py` | the tool that made `crops/` |

The pictures of the Edge and Server configurations stay on the machine (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-01-11c\shots`).
They are not committed: their records hold every state, and they read the same as Chrome on WebAssembly.

## Environment

| | |
|---|---|
| Windows | Windows 11, one display at **150%** (144 DPI), light mode, high contrast off, regional format en-GB, keyboard English (UK) |
| Display scale | **150%, not changed.** The 100% readings are at the browser's zoom of 100% (`devicePixelRatio` 1.5), as Part A's Excel pictures were at Excel's zoom of 100% on this display. The 150% readings are at the browser's zoom of 150%, set with real keys (Ctrl+= three times: 110%, 125%, 150%; `devicePixelRatio` 2.25), as Part A's were at Excel's zoom of 150%. Windows' display scale was not used for them: changing it needs a sign-out here, which ends this session |
| Browsers | Chrome 153.0.8010.54 and Edge 154.0.4258.48, the Windows builds, headed, through Playwright 1.62.1 from portable Node v24.14.1. Window 1700 × 1360 DIP at (40, 20), page 1686 × 1266 CSS px |
| Hosts | Built from `c09c759` in WSL2 (.NET SDK 10 through nix): WebAssembly (`samples/ExGrid.DemoHost`) on `localhost:5299`, Server (`samples/ExGrid.DemoHost.Server`) on `localhost:6298` |
| The page's culture | **en-US.** `SheetPage.razor` gives every Sheet on `/sheet` the culture en-US, so case 18 compares with Part A's en-US answers (case 20) |

**The configurations.**

| Record | Browser, host | Chrome, scheme, zoom | Cases |
|---|---|---|---|
| `wasm-chrome` | Chrome, WebAssembly | built-in, light, 100% | every case |
| `wasm-chrome-extra`, `wasm-chrome-extra-12x` | the same | the same | the additions 7x and 12x |
| `wasm-chrome-z150` | the same | built-in, light, 150% | 8, 9 |
| `wasm-chrome-mud` | the same | ExSheet.MudBlazor, light, 100% | 22, 24, 25, and 11 (added) |
| `wasm-chrome-dark` | the same | built-in, `&scheme=dark`, 100% | 1, 4, 9 |
| `wasm-msedge`, `server-chrome`, `server-msedge` | Edge on WebAssembly; Chrome and Edge on Server | built-in, light, 100% | 8, 9, 11, 16, 17, 18 |
| `wasm-msedge-z150`, `server-chrome-z150`, `server-msedge-z150` | the same | built-in, light, 150% | 8, 9 |

## Method

- **Input.** Every key and click went through `input-server.ps1`: `SendInput` with a virtual-key and a scan
  code per key, and the real mouse.
  - A character key (`&`, `_`, `~`, `$`, `+`, …) was sent as the key that types it on the UK layout, with
    the Shift it needs, as Part A's were.
  - Cells were reached with Part A's keys, from A1, after a click on A1. Format Cells was opened with
    Ctrl+1, and its tabs, choices and buttons were clicked.
  - Playwright opened the pages and read them. It sent no input.
- **Screen pixels** were matched to the page on every page opened. The probe moves the pointer and reads
  where the page saw it.
- **Pictures and pixels.**
  - At 100%: Playwright's screenshot of the Sheet at the device's scale.
  - At 150%: the browser's own surface (`Page.captureScreenshot` through the DevTools protocol), because
    Playwright's screenshot at a browser zoom is resampled to 1.5 px per CSS px.
  - **Across an edge**, the pixels run from 6 device px before it to 6 after, at a quarter, a half and
    three quarters along it. Offset 0 is the first device pixel past a cell's bottom or right edge, so
    **`-1` is the cell's last pixel, the gridline it holds**. For a top or left edge, 0 is the cell's
    first pixel and `-1` the one before it. Excel's tables count from `PointsToScreenPixels`, and
    Part A named the gridline's offset in each.
  - **Along a line**, each device row from `-4` to `+3` with a dark pixel (luminance below 128) gives its
    runs (`+9 -3`), over the cell's width less 2 px at each end. The first run is cut by where the
    reading starts.
  - **A cell's text colour** is the most frequent pixel unlike its ground, as Part A's.
- **The Selection was parked at F10** (Ctrl+Home, Down ×9, Right ×5) where a case reads lines with the
  Selection away. Part A parked at G14 with Esc first. Here Esc moved the keyboard off the Sheet. And
  G14 scrolled the Sheet by part of a row, which put row 2's top under the Column Headings.
- **Case 9 at A16.** The Sheet shows fewer rows than Excel did, so case 9 was read again with the Focus
  moved to A16, which scrolls B13 and B14 into view.
- **Cases 24 and 25 on `?case=16`** (A1 `abc`, A2 `def`, as Part A's group 4). Their set-up was made with
  ExSheet's own keys and Format Cells: Ctrl+B, Ctrl+I, and Format Cells' Fill and Border tabs.
  - On `/sheet` itself, A1 and A2 hold the page's own table.
- **Additions**, each marked in the records (`extra`):
  - **7x:** case 7 with both edges set by Format Cells.
  - **12x:** case 12's insert through the Context Menu.
  - **11 under the Mud Chrome.**
- **Faults found in trials, before the recorded runs.** Each is fixed in the probe as committed, and
  the superseded records stay on the machine.
  - Clicks meant for Format Cells landed below what showed of it; the probe now scrolls to a control
    with the wheel.
  - Escape left the Sheet (above).
  - Case 12's zoom carried into the later pages.
  - MudBlazor's dialog was clicked while it was still moving into place.
  - The 150% pictures were resampled.

## Case by case

Excel's pictures are Part A's, in `../2026-10-01-windows-excel-11/shots/`, and the twelfth run's in
`../2026-10-01-windows-excel-12/shots/`. ExSheet's are this run's `crops/` (cut and enlarged) and `shots/`
(the whole Sheet). The configuration is `wasm-chrome` unless named. Offsets are device pixels; "g" is the
gridline's pixel.

| Case | Excel | ExSheet | Same or different |
|---|---|---|---|
| 1 | Each value in its colour: `#000000`, `#0000FF`, `#00FFFF`, `#00FF00`, `#FF00FF`, `#FF0000`, `#FFFFFF` on black, `#FFFF00`, in both columns (`1-set-x4.png`) | the same eight, in both columns; `[White]` on `#000000` (`crops/1-set-wasm-chrome-x2.png`; dark: `…-dark-x2.png`) | **Same** (dark: the same) |
| 2 | A1 `#FF0000`, B1 `#0000FF` (`2-set-x4.png`) | A1 `#FF0000`, B1 `#0000FF` (`crops/2-set-wasm-chrome-x4.png`) | **Same** |
| 3b | none red: `abc`, `TRUE`, `#DIV/0!`, `5` all `#000000`; `#DIV/0!` has a green triangle (`3b-set-x4.png`) | all four `#000000`; no triangle (`crops/3b-set-wasm-chrome-x4.png`) | **Same** colours; the triangle is not drawn |
| 3c | `#########` in `#FF0000` at width 9.5 (`3c-hashes-x4.png`) | `######` in `#FF0000` (`crops/3c-set-wasm-chrome-x4.png`) | **Same colour; different count of `#`** (difference 4) |
| 4 | The Fill covers all four gridlines; the pixel past each is the neighbour's white (`4-filled-x4.png`) | the same: g yellow on all four sides (top `-1..6 ffff00`, bottom `-6..-1 ffff00`), white past it (`crops/4-set-wasm-chrome-x4.png`; dark the same) | **Same** |
| 5 | White Fill: the four gridlines gone (`5-filled-x4.png`) | `-6..6 ffffff` on all four (`crops/5-set-wasm-chrome-x4.png`) | **Same** |
| 6 | Yellow throughout between B2 and C2 (`6-filled-x4.png`) | `-6..6 ffff00` (`crops/6-set-wasm-chrome-x4.png`) | **Same** |
| 7 | B2's right thick red: g-1..g+1 red. Then C2's left thin blue: blue on g alone, no red (`7-b2-right-red-x4.png`, `7-c2-left-blue-x4.png`) | the page's B2 right: `-2..0 ff0000` (g-1..g+1). After C2's left thin `#0000FF` by Format Cells: `-1 0000ff` (g), no red (`crops/7-set-…-x4.png`, `crops/7-parked-…-x4.png`). 7x, both by Format Cells: the same at each step (`crops/7x-b2-parked-…`, `crops/7x-c2-parked-…`) | **Same** |
| 8, 100% | Thick across both, centred: g-1..g+1; rows 2 and 3 raised to 15 pt (`8-c-set-x4.png`) | `-2..0 000000`, g at `-1`: centred; rows 28 CSS px (`crops/8-set-wasm-chrome-x4.png`) | **Same line; one row height** (deliberate, difference 1) |
| 8, 150% | g-1..g+1, centred (`8-z150-c-set-x4.png`) | g is 2 px (`-2..-1`); the line `-2..0` (`crops/8-set-wasm-chrome-z150-x4.png`) | **Different** (difference 6) |
| 9, 100% | The table in Part A's report: hair, thin, dotted, dashed, dash-dot and dash-dot-dot on g; medium and the medium dashes on g-1..g; thick g-1..g+1; double g-1 and g+1 with g white; dashes 8 (dash families), 3/1, 2/2 (`9-c-set-x2.png`) | the same positions for all thirteen; hair 1/1 (off pixels gridline grey), dotted 2/2, dashed 3/1, every long dash 9 with gaps and dots 3; slanted 11,1,5,1 over 10,2,4,2 (`crops/9-set-wasm-chrome-x2.png`, `crops/9-scrolled-wasm-chrome-x2.png`) | **Same positions; long dash 9 against the table's 8** (difference 5); one row height (deliberate) |
| 9, 150% | the same positions, long dash 9 (`9-z150-c-set-x2.png`) | difference 6's table (`crops/9-set-wasm-chrome-z150-x2.png`, `crops/9-scrolled-wasm-chrome-z150-x2.png`) | **Different** (difference 6) |
| 10 | The line above B3's Fill: black inside B3, then yellow (`10-c-set-x4.png`) | `-2..0 000000 \| 1..6 ffff00` (`crops/10-set-wasm-chrome-x4.png`) | **Same** |
| 11 | Outline `#217346`, 2 px, over the outer lines; inner lines black above `#C7C7C7`; B2 white (`11-b-selected-x4.png`) | outline `#000000` 3 px (Mud: `#594AE2`) over the outer lines; inner lines `#0B1627` (Mud `#100D29`) under `#DCE7F8` (Mud `#E1DEFA`); B2 white (`crops/11-selected-wasm-chrome-x4.png`, `…-mud-x4.png`) | **Different look** (difference 7) |
| 12 | A3, Shift+Space, Ctrl+Shift+=: a row inserted; B3 yellow; B3's top the thick line; no bottom (`12-b-inserted-x4.png`) | no row inserted; Chrome zoomed to 110% (`crops/12-inserted-wasm-chrome-x4.png`). 12x, from the Context Menu: B3 yellow, the thick line between B2 and B3, no bottom (`crops/12x-parked-wasm-chrome-x4.png`) | **Different key** (difference 3); **the insert itself the same** (12x) |
| 13 | `&`: the outer edges of B2:D4 only. `_`: every edge cleared (`13-b-amp-x2.png`, `13-b-underscore-x2.png`) | parked after `&`: B2:D4's outer edges black on g, inner edges the gridline; after `_`: all gridline (`crops/13-amp-parked-…-x2.png`, `crops/13-underscore-parked-…-x2.png`) | **Same** |
| 14 | Each range its own outline; nothing between or inside (`14-b-amp-x2.png`) | parked: B2:C3 and E5:F6 each outlined on g; B2's right, C3's top, E5's right and F6's left the gridline (`crops/14-parked-wasm-chrome-x2.png`) | **Same** |
| 12-1 | The thick red line drawn, `-3..-1 ff0000` (g-1..g+1) (`../2026-10-01-windows-excel-12/shots/1-parked-x4.png`) | `-2..0 ff0000` (g-1..g+1); nothing of C2's blue (`crops/12-1-parked-wasm-chrome-x2.png`) | **Same** |
| 12-14 | No line at A3's left beyond the Headings' edge; row 3's top and bottom drawn (`../2026-10-01-windows-excel-12/shots/14-parked-x4.png`) | A3's left reads as A5's (the Headings' edge, `#C6C6C6`); row 3's top and bottom black on g (`crops/12-14-parked-wasm-chrome-x4.png`) | **Same** |
| 16 | Each of Ctrl+B, 2, I, 3, U, 4, 5 toggles; underline single; the second press takes it off (`16-ctrl-<key>-<n>-x4.png`) | the same for all seven; then Ctrl+Z undid one press at a time (`crops/16-ctrl-<key>-<n>-…-x4.png`, `…-undo-<n>-…`) | **Same** |
| 17 | Focus A1 (bold): both plain. Focus A2 (plain): both bold (`17-focus-a1-ctrl-b-x4.png`, `17-focus-a2-ctrl-b-x4.png`) | the same (`crops/17-focus-a1-ctrl-b-…-x4.png`, `crops/17-focus-a2-ctrl-b-…-x4.png`) | **Same** |
| 18 | en-GB (case 18): General, `1,234.50`, `12:00`, `18-May-03`, `£1,234.50`, `123450%`, `1.23E+03`; en-US (case 20): `12:00 PM`, `$…` (`18-uk-char-<name>-x2.png`) | under the page's en-US: `1234.5`, `1,234.50`, `12:00 PM`, `18-May-03` (column 99 → 108 CSS px), `$1,234.50 ` (99 → 104), `123450%`, `1.23E+03`; each undone by Ctrl+Z (`crops/18-<name>-wasm-chrome-x4.png`) | **Same as Excel under en-US** |
| 22 | Tabs: Number, Alignment, Font, Border, Fill, Protection; Number first; the second Ctrl+1 on the tab last shown; the twelve categories; the line styles in two columns of seven, Thin selected; presets None, Outline, Inside (Inside disabled); eight edge buttons (`22-<state>-window-1.png`) | five tabs, no Protection; Number first; the second Ctrl+1 on Fill, the tab last shown; the same twelve categories; the same fourteen styles in order, Thin selected; the same presets, Inside disabled; six edge buttons, Horizontal and Vertical disabled. Both Chromes (`shots/22-<state>-wasm-chrome.png`, `…-mud.png`) | **Same but for Protection and the diagonals** (difference 8, recorded by ADR-0071) |
| 24 | Font: style box empty; *Normal font* mixed. Fill: No Colour. Border: no outer line, the inside horizontal grey dotted, Horizontal `On`, Inside enabled (`24-tab-*-window-1.png`) | Font: no style chosen. Fill: No Colour. Border: no outer line, the inside horizontal grey dotted (`#808080`, dash `1 2`), Horizontal `mixed`, Inside enabled. Both Chromes (`shots/24-tab-*-wasm-chrome.png`, `…-mud.png`) | **Same but for *Normal font*** (difference 9) |
| 25 | Only the colour changed: A1 bold red, A2 italic red (`25-after-ok-x4.png`) | the same, both Chromes (`crops/25-ok-wasm-chrome-x4.png`, `…-mud-x4.png`) | **Same** |

## The machine afterwards

- No probe, input helper or Playwright browser is running. The user's Chrome and the Stream Deck's `node`
  were left alone.
- The DemoHosts in WSL are stopped.
- The keyboard is English (UK), `0x08090809`, on every window; each probe put it back at its end.
- The regional format is en-GB. The display is at 150%, unchanged.
- No Excel ran in this part.
