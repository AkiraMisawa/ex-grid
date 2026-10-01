# Verification — 2026-09-30, Windows, Excel only: completion, the pointed shade, the Formula Bar's keys (tenth run)

**Scope: [`verify-on-windows-10.md`](../../docs/specs/exsheet/verify-on-windows-10.md), groups 1–3,
and group 4, which the procedure gained after them.** Excel alone was asked, so nothing was built.

- Group 1 asks about completion: the readings in
  [ADR-0058](../../docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md), "Readings,
  until Excel is observed".
- Group 2 asks for the shade of the Reference Point is writing. It belongs to ADR-0057, "What Part B of
  the eighth Windows run settled", and ticket 43. Both are on `claude/exsheet-eighth-run-b`.
- Group 3 asks about the Formula Bar's keys. It belongs to ADR-0051's note "An edit in the Formula Bar
  never enters Overwrite", ticket 42 and ED-29, all on the same branch.
- Group 4 asks what F2 does in the Formula Bar, and what the keys after it do. It decides ADR-0051's
  rule for F2 there (ticket 42).

**Verified commits.**

- Groups 1–3: **`261666c0bdd4b75fd3548e6e26ec0306eca2a0ff`**, the tip of
  `claude/exsheet-pointing-scope` when this run began. The results are committed on
  `claude/exsheet-windows-verify-10`, branched from that commit.
- Group 4: **`632b323eb52a4339ae072a87c42e60044fc28a71`**, the tip of the same branch when group 4
  ran. That commit added group 4 to the procedure. The results are a second commit on
  `claude/exsheet-windows-verify-10`.

Nothing here changes an ADR, `CONTEXT.md` or the Definition of Done.

Files beside this one:

- `excel-only.ps1`: the script. It began as the ninth run's `pointing.ps1`.
  - `-Case 1,2` runs cases of the procedure's tables.
  - `-Pass b` names a further pass's files apart (`7-b-…`).
  - `-Theme Black` sets File › Account › Office Theme with the real mouse, and does nothing else.
- `excel-only.jsonl`: one line per case and pass. The environment is case `0`, and each theme change
  is a `theme` line. For each state it holds:
  - what UI Automation read: the Formula Bar's text and selection, with the selection's offsets (a
    caret is a selection of length 0), the status bar's mode, the focused element, and every popup
    window of Excel's with its UI Automation tree;
  - `list`: the rows of the completion list, and the one selected;
  - `text`: the colours of the text and of its ground, read from the 600 ms picture along row 10 from
    D10 rightwards (the edit in the cell) and along the Formula Bar's line;
  - `outlines`: the pixels that changed on the edges of A1, B1, C1, E1, F1, G1, D10, D11 and D12.
- `shots/`: for each state `<case>[-<pass>]-<state>`:
  - the window's top left (`.png`);
  - crops of the Formula Bar, the Name Box, the status bar's mode and the cells;
  - each popup;
  - the text of the cell and of the Formula Bar enlarged four times (`-cell-text-x4`,
    `-bar-text-x4`).

## Environment

- **The machine of the earlier runs.** Windows 11 with one display, 3840×2160 at **150%** (144 dpi;
  the work area is 3840×2088). Windows is in **light mode** (`AppsUseLightTheme` 1,
  `SystemUsesLightTheme` 1). High contrast is off. The regional format is en-GB.
- **Excel: Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, 64-bit**,
  the eighth and ninth runs' build.
  - Settings: Edit directly in cell on, Formula AutoComplete on, Function ScreenTips on, "Use table
    names in formulas" on. The standard font is Aptos Narrow 11.
- **The Office Theme was "Use system setting"**, read in File › Account through UI Automation. There
  was no `UI Theme` value in the registry.
  - For group 2's second pass it was set to **"Black"** (19:44:37). It was set back to **"Use system
    setting"** at 19:59:02. Both changes went through File › Account with the real mouse.
  - The list offered `Dark Grey`, `Black`, `White`, `Use system setting` and `Colourful`.
  - Afterwards: see "The machine afterwards".
- **The keyboard.** Each case's Excel started with English (UK), 0x08090809, as did the script's own
  thread. No IME belongs to that keyboard.
  - The script posted English (UK) to each case's Excel anyway, and restored what it had found.
  - **The keys arrived as typed**: `'=SUM(Positions[PV])+XLOOKUP(1,A2:A4,B2:B4,,0,-1)` typed into D10
    and entered read back identical through COM (case 0).
- **Input.**
  - Keys: `SendInput`, with a virtual-key and a scan code per key.
  - A click: the pointer travels from where it is in 15 steps of 15 ms, then presses for 100 ms.
- **Pictures.** `PrintWindow` (PW_RENDERFULLCONTENT), each window of Excel's laid at its place.
  - The File › Account page was never pictured, because it shows the account.
  - The committed pictures stop short of the title bar's right end, where the account's initials are.

## Method

1. **An Excel of the script's own for every case.** It is started with `New-Object`, and its process
   is checked to be new. The script never attaches to a running Excel. Each Excel holds a new
   workbook, Book1, with one sheet, `Sheet1`, maximised at 100% zoom:
   - A1:B4 is the Table `Positions` (`Id`: R-1, R-2, R-3; `PV`: 10, 20, 30);
   - D10 is selected.
2. **Each state** is the case's keys and clicks, then:
   - a picture at 600 ms, and another at 900 ms;
   - then what UI Automation reads.

   After the last state, Escape until Excel answers COM. Then the active cell, D10's formula and the
   used range are read through COM.
   - **In groups 1–3, every case ended with the active cell D10, D10 empty, and the used range
     A1:B4.** Nothing was committed in any case.
   - **In group 4, cases 20–22 entered the Formula into D10.** Their tables below say so.
3. **The caret** is the Formula Bar's selection, read through its TextPattern: the text before the
   selection's start, counted.
   - The Formula Bar reports it while the edit is in the cell too, as the eighth run found.
   - In the tables, "caret 6" means six characters lie before it. A selection is written 24–27.
4. **The mode** is the status bar's first pane, read through UI Automation ("Cell Mode Enter").
5. **The Name Box.** UI Automation did not find it, as in the ninth run's pass a. Its crop is cut at
   the place the ninth run's pass b found, (16,283)–(134,311).
   - While a Formula is edited, it shows a function name (`SUM`), not the active cell. The active cell
     is therefore read from the pictures (the cell holding the edit) and from COM after Escape.
6. **The text's colours (group 2)** are read as the eighth run read case 20x:
   - each column of the text stands for its darkest pixel;
   - a run of columns whose pixel is saturated gives a colour;
   - the ground behind the text is each column's lightest pixel, and a run of ground that is not the
     editor's white is a highlight.

   The same readings were also taken for any ground (each column's most frequent pixel as its ground,
   the pixel furthest from it as its text), for the Black theme. Excel's cells stayed white under
   Black, so both readings agree.
7. **Additions**: cases or states not in the procedure. They are marked *(addition)*:
   - `7x`, `8x`: the eighth run's keys for the first two shades, as a control;
   - `7s`, `8s`: cases 7 and 8 with a wait of 1 s before the ↓;
   - further passes of 7–13;
   - a second pass of 20–25 (pass b).
8. **A trial of case 1** (19:32:49), before the recorded run, gave what case 1 gave in the run. It is
   not in the `.jsonl`.

## Group 1 — completion

The run: 19:33:25–19:34:29. In every state the mode was Enter, except where the table says otherwise.

| # | Keys typed into D10 | What is asked | Reading | Excel | Agrees |
|---|---|---|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,0` | With the caret after the `0`: is a list shown? Its items | no list | **A list of one item, `0 - Exact match`, selected.** Caret 25, after the `0`. The item's tip: "Searches for an Exact match, if not found return #N/A". The ScreenTip shows `XLOOKUP(…)` with `[match_mode]` in bold | **no** |
| 2 | `=XLOOKUP(1,A2:A4,B2:B4,,-` | The list and its items | `-1 - …` only | **All five, with `0 - Exact match` selected**: `0 - Exact match`, `-1 - Exact match or next smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`, `3 - Regex match`. Caret 25, after the `-` | **no** |
| 3 | `=XLOOKUP(1,A2:A4,B2:B4,,`, `{DOWN}`, `{TAB}` | The item selected after ↓; the text after Tab; is a list still open | `-1` written; no list | After `,,`: the same five, `0 - Exact match` selected. **After ↓: `-1 - Exact match or next smaller item` selected**, and its tip replaces the first. **After Tab: `=XLOOKUP(1,A2:A4,B2:B4,,-1`**, caret 26 (the end), **no list**. The function's ScreenTip stays | yes |
| 4 | `=SUM(Positions[`, `{DOWN}` until `PV` is selected, `{TAB}` | The text after Tab (with or without `]`); is a list still open, and its items | `=SUM(Positions[PV`; the list still open on `PV` | The list: `@ - This Row` (selected), `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals`. The first ↓ selects `Id` and the second selects `PV`. **After Tab: `=SUM(Positions[PV`, without `]`**, caret 17 (the end), and **no list**. The `SUM` ScreenTip stays | the text yes; the list **no** |
| 5 | `=Posit`, `{TAB}` | The text after Tab; is a list still open, and its items | `=Positions`; the list still open on `Positions` | The list: `Positions` (selected), alone. **After Tab: `=Positions`**, caret 10 (the end), and **no list**: no popup of any kind | the text yes; the list **no** |
| 6 | `=XLOOKUP(1,A2:A4,B2:B4,,`, `{RIGHT}` | With the value list open: does → move the caret, point, or choose? | the caret moves (nothing to its right, so it stays) | **→ points.** The text becomes `=XLOOKUP(1,A2:A4,B2:B4,,E10` in mode **Point**. `E10` is shown selected (UI Automation's selection 24–27), on the grey ground in the third colour's dark shade (`#44007c` on `#c6c6c6`). E10 is outlined, dashed, with the third colour's corner squares (`6-right-cell-text-x4.png`). The list closes. A value tip `0` shows above | **no** |

## Group 2 — the pointed Reference's shade

The pointed Reference is D11 in every case: after the operator, ↓ points from D10. The Reference is
the n-th one in the Formula, so it takes the n-th colour. Passes: `a` at 19:35:04–19:36:04; `b`, `c`,
`d` of 7 and 8 and the additions at 19:36:56–19:39:01; `b` of 9–13 at 19:43:46–19:44:29; `black`
(Office Theme "Black") at 19:45:00–19:47:23.

### The shades

Every colour is read in the cell, from the 600 ms picture. **Under Black, every value in the first
three columns was the same as under "Use system setting"**: the cells stay white, and so do the
ground and the texts. Only the outlines drawn on the grid change (next table).

| n | Case | Formula | Ground under the pointed Reference | The pointed Reference's text | The other References' text, in order | Where it was read |
|---|---|---|---|---|---|---|
| 1 | 7 | `=1+D11` | `#c6c6c6` | **`#0401a2`** | — | `7-c`, `7s`, `7s-black`, and the controls `7x`, `7x-black` (`=SUM(D11`). **Not shown in `7`, `7-b`, `7-d`, `7-black`**, below |
| 2 | 8 | `=A1+D11` | `#c6c6c6` | **`#630101`** | `#326ac7` | `8-b`, `8-c`, `8-d`, `8-black`, `8s`, `8s-black`, and the controls `8x`, `8x-black` (`=D11+D12`). **Not shown in `8`** (pass a), below |
| 3 | 9 | `=A1+B1+D11` | `#c6c6c6` | **`#44007c`** | `#326ac7`, `#c0353e` | `9`, `9-b`, `9-black` |
| 4 | 10 | `=A1+B1+C1+D11` | `#c6c6c6` | **`#003600`** | `#326ac7`, `#c0353e`, `#8157b7` | `10`, `10-b`, `10-black` |
| 5 | 11 | `=A1+B1+C1+E1+D11` | `#c6c6c6` | **`#550059`** | `#326ac7`, `#c0353e`, `#8157b7`, `#007c20` | `11`, `11-b`, `11-black` |
| 6 | 12 | `=A1+B1+C1+E1+F1+D11` | `#c6c6c6` | **`#531c00`** | `#326ac7`, `#c0353e`, `#8157b7`, `#007c20`, `#b03e84` | `12`, `12-b`, `12-black` |
| 7 | 13 | `=A1+B1+C1+E1+F1+G1+D11` | `#c6c6c6` | **`#00323f`** | `#326ac7`, `#c0353e`, `#8157b7`, `#007c20`, `#b03e84`, `#b64900` | `13`, `13-b`, `13-black` |

Notes on the readings:

- **The grey lies under the whole Reference**, 36 or 37 columns wide at 150%, across the three
  characters `D11`.
  - UI Automation gives the Formula Bar's selection as those three characters wherever the grey
    shows: 7–10 in case 9, 19–22 in case 13, 3–6 in `7-c`.
  - Where the grey does not show, the selection is the caret at the end (6 in `7`, 7 in `8`).
- **`#003600` (the fourth)** is dark enough that the eighth run's rule reads it as "dark" rather than
  "colour": its saturation is 54, and the threshold is 60. It is the darkest pixel of each column
  across the whole of `D11`, and the enlargement shows a dark green (`10-pointed-cell-text-x4.png`).
- **For the sixth and seventh**, the `D` and each `1` read as separate runs of the same colour
  (`#531c00`, `#00323f`).
- **The Formula Bar showed no colour and no grey in any group 2 state**, since the edit was in the
  cell. Its text was `#242424` on white, and white on `#292929` under Black.

**Whether the grey shows at all, in cases 7 and 8.** The same keys did not always give it:

| Keys | Grey and dark shade shown | Not shown |
|---|---|---|
| `=1+`, ↓ (case 7) | `7-c` | `7` (a), `7-b`, `7-d`, `7-black` |
| `=1+`, a wait of 1 s, ↓ (`7s`, *addition*) | `7s`, `7s-black` | — |
| `=A1+`, ↓ (case 8) | `8-b`, `8-c`, `8-d`, `8-black` | `8` (a) |
| `=A1+`, a wait of 1 s, ↓ (`8s`, *addition*) | `8s`, `8s-black` | — |
| `=SUM(`, ↓ (`7x`, *addition*; the eighth run's case 29) | `7x`, `7x-black` | — |
| `=D11+`, ↓↓ (`8x`, *addition*; the eighth run's case 32) | `8x`, `8x-black` | — |
| cases 9–13 | every pass (a, b, black) | — |

- **Where it was not shown**, `D11` wore the plain first or second colour (`#326ac7`, `#c0353e`) on
  white. No value tip showed, and the caret stood at the end. The outline and the green dashes on D11
  were drawn as in the other passes.
- **Where it was shown**, a value tip (`0`) showed above the edited cell, as in the ninth run.
- The keys were the same in every pass: `SendInput`, 30 ms apart.

### The outlines on the grid, by theme *(seen; not asked)*

Read on D11, the pointed cell, in cases 7–13: the line along its bottom edge, and the fill inside it.
The green dashes over Point's outline were `#217346` under both themes.

| n | Line, "Use system setting" | Fill | Line, "Black" | Fill |
|---|---|---|---|---|
| 1 | `#326ac7` | `#ebf0f9` | `#5b97ff` | `#eff5ff` |
| 2 | `#c0353e` | `#f9ebec` | `#ff616b` | `#ffeff0` |
| 3 | `#8157b7` | `#f2eef8` | `#b77cff` | `#f8f2ff` |
| 4 | `#007c20` | `#e6f2e9` | `#00b02c` | `#e6f7ea` |
| 5 | `#b03e84` | `#f7ecf3` | `#fc58be` | `#ffeef9` |
| 6 | `#b64900` | `#f8ede6` | `#ff9000` | `#fff4e6` |
| 7 | `#267392` | `#e9f1f4` | `#2eb0b3` | `#eaf7f7` |

**Under Black, the outlines on the grid take lighter colours, while the same References' text in the
cell keeps the "Use system setting" colours.** Compare `13-pointed-cells.png` and
`13-black-pointed-cells.png`.

## Group 3 — the Formula Bar's keys

The run:

- pass a at 20:01:07–20:02:22;
- probes at 20:03;
- pass b of 14–17 at 20:04:15–20:05:00.

**Cases 14–17 are read from pass b**, for the reason below.

A click into the empty Formula Bar lands 200 px right of its left edge. A click "after `B1`" lands
400 px right of it, past the text's end. A click back into D10's own text lands 30 px inside D10,
between `=A` and `1+B1`.

| # | Keys and clicks | What is asked | Reading | Excel | Agrees |
|---|---|---|---|---|---|
| 14 | Click into the empty Formula Bar, type `=A1+B1`, then `{HOME}` | The mode after typing and after Home; the caret; the active cell; is the edit still open | Edit throughout; the caret at 0; D10 still edited | After typing: **Edit**, caret 6. After Home: **Edit**, **caret 0**, the focus still in the Formula Bar. D10 holds the edit: its text shows in the cell, uncoloured, and nothing was committed (D10 empty and active after Escape) | yes |
| 15 | As 14, then `{RIGHT}{DEL 3}` | The text | `=B1` | **`=B1`**, caret 1, Edit | yes |
| 16 | As 14 without Home, then `{END}`, `{LEFT}`, `{LEFT}` | The caret after each | at the end, then one and two to the left | **6, 5, 4**, Edit throughout | yes |
| 17 | As 14 without Home, then `{F2}` | The mode before and after F2 | open | Before: **Edit**. After F2: **Enter**. The caret stays at 6, and the focus stays in the Formula Bar | (open) |
| 18 | Type `=A1+B1` into D10, then click into the Formula Bar after `B1`; then `{HOME}` | The mode before and after the click; then Home: the caret, the active cell | Enter, then Edit; Home moves the caret | Typed: **Enter**, caret 6, coloured in the cell. After the click: **Edit**, caret 6, the focus in the Formula Bar. The colours move to the bar (`#006cbe`, `#bc2f34`), and the cell's text is plain. **Home: caret 0**, Edit, D10 still holds the edit | yes |
| 19 | As 18 (without its Home), then click back into D10's own text; then `{LEFT}` | The mode after the click; then Left: does it move the caret or commit and move the cell | Edit stays; the caret moves | After the click into D10: **Edit**, caret 2 (after `=A`). The focus is in the cell, and the colours are back in the cell. **Left: caret 1**, Edit, nothing committed | yes |

**Why 14–17 were run again.** In pass a, each state was read straight after the click into the empty
Formula Bar (pictures and UI Automation, about 1.5 s). Then:

- the keys typed next did not appear anywhere;
- the focus moved to the Name Box (UI Automation's focused element was the `Edit` of class `Edit`, and
  the pictures show the Name Box's focus border);
- the mode stayed Edit, with the bar empty.

Two probes (20:03) placed it:

- `p1` clicked and typed with no reading between, and `=A1+B1` landed.
- `p2` read after the click, and then neither `1` nor `2` landed.

Pass b therefore clicks and types as one state. The procedure asks for nothing between the click and
the typing. Pass a's lines and the probes stay in the `.jsonl` (`group` `bar` pass `a`, `group`
`probe`). Cases 18 and 19 read a state after a click into a bar that already held text, and their
next keys landed.

## Group 4 — F2 in the Formula Bar, and the keys after it

The procedure gained this group at `632b323`, after groups 1–3. The run:

- pass a at 20:22:33–20:23:40;
- pass b, the same six cases again, at 20:23:50–20:24:57 *(addition)*.

**Both passes gave the same modes, texts, carets, active cells and cells written, state for state.**
The table is pass a. The `.jsonl` lines are `group` `bar-f2`.

The click into the empty Formula Bar and the typing are one state, with no reading between them, as in
pass b of 14–17. The click lands 200 px right of the bar's left edge. In every case the keys landed.

The procedure gives no reading for this group, so the table has no "Agrees" column. "Open" means Excel
was still editing D10 when the state was read. "Entered" means the edit closed and the Formula was
written into a cell.

| # | Keys and clicks | What is asked | Excel |
|---|---|---|---|
| 20 | Click into the empty Formula Bar, type `=A1+B1`, `{F2}`, then `{HOME}` | The mode after F2 and after Home; the Formula Bar's text and caret; the active cell; is the edit still open, or was the Formula entered (and into which cell) | Typed: **Edit**, `=A1+B1`, caret 6, the focus in the Formula Bar. After F2: **Enter**, `=A1+B1`, caret 6, the focus still in the bar, open. **After Home: Ready. The Formula was entered into D10**, which shows `#VALUE!`. **The active cell is A10**, and the Formula Bar shows A10's empty contents. The focus is on the sheet's cell |
| 21 | As 20 without Home, then `{RIGHT}` | The same | After F2: **Enter**, caret 6, open. **After →: Ready. Entered into D10** (`#VALUE!`). **The active cell is E10**, and the bar is empty |
| 22 | As 20 without Home, then `{DOWN}` | The same | After F2: **Enter**, caret 6, open. **After ↓: Ready. Entered into D10** (`#VALUE!`). **The active cell is D11**, and the bar is empty |
| 23 | As 20 without Home, then `{F2}` again | The mode after the second F2 | After the first F2: **Enter**, caret 6. After the second: **Edit**, `=A1+B1`, caret 6. The focus stays in the Formula Bar and the edit stays open. Nothing was entered (D10 empty after Escape) |
| 24 | Click into the empty Formula Bar, type `=A1+`, `{F2}`; then `{DOWN}` | The mode after F2 (Point, Enter or Edit); then Down: the text (is `D11` written?) and the mode | Typed: **Edit**, `=A1+`, caret 4. **After F2: Enter** (not Point), `=A1+`, caret 4. **After ↓: Point. `D11` is written**: the bar holds `=A1+D11` with `D11` selected (24–27). D11 is outlined in the second colour's dashes (`#c0353e`, fill `#f9ebec`). The active cell is still D10, the edit is open and the focus stays in the Formula Bar. A value tip `0` shows above the bar. Nothing was entered (D10 empty after Escape) |
| 25 | Type `=A1+B1` into D10 (the cell), `{F2}`, `{F2}` | The mode after each F2, in the cell: the cell's own cycle, for comparison | Typed: **Enter**, caret 6, the focus on the sheet (the edit is in the cell). After the first F2: **Edit**, caret 6. After the second: **Enter**, caret 6. The References stay coloured in the cell throughout (`#326ac7`, `#c0353e`). Nothing was entered |

What the states show besides, and nothing asks:

- **The Formula Bar's F2 cycle is Edit → Enter → Edit** (23). The cell's is Enter → Edit → Enter (25).
  A click into the empty bar starts at Edit; typing into the cell starts at Enter.
- **The caret does not move on F2**, in the bar or in the cell (20–25).
- **F2 does not move the edit between the bar and the cell.** After F2 in the bar, the References
  stay coloured in the bar (`#006cbe`, `#bc2f34`) and the cell's text stays plain, and the focus stays
  in the bar. In the cell (25) they stay coloured in the cell.
- **The pointed `D11` in the bar (24)** is drawn light on a `#616161` ground. In the cell, group 2
  read the pointed Reference on `#c6c6c6`. This was not asked, and the bar's colour was read only
  roughly (the text's pixels mix with the grey).

## Where Excel and the readings differ

Each item is a difference from a reading of the procedure, or from a statement an ADR makes about
Excel. Nothing here is decided.

1. **A value typed whole still lists it** (case 1). With `0` typed at `match_mode`, the list shows
   `0 - Exact match` alone, selected. ADR-0058, "Readings, until Excel is observed": "A value typed
   whole lists nothing."
2. **`-` lists every value, with `0` selected** (case 2), not only those that begin with `-`. The
   same ADR-0058 paragraph: "a prefix of a value, such as `-`, still lists the values it begins."
3. **Tab on a column name closes the list** (case 4). The text agrees: `=SUM(Positions[PV`, without
   `]`. ADR-0058, same section, "As built, and asked with them": "Accepting a table's or a column's
   name leaves the list open on that same name."
4. **Tab on a table's name closes the list** (case 5). The text is `=Positions`. Same sentence of
   ADR-0058.
5. **→ with the value list open points** (case 6). It writes `E10` in the third colour, shown
   selected, and the list closes. Two places say otherwise:
   - ADR-0058, same section: "while any list is open, ← and → move the caret (ADR-0051), so → does
     not point from an open value list";
   - ADR-0051, "Added while building, second round": "While the completion list is open, only ↑, ↓,
     Tab and Escape are claimed. ← and → move the caret, as they do in Excel."
6. **`=1+D11` did not always show the grey and the dark shade** (case 7). They showed in 1 of 5
   passes with the procedure's keys, and in both passes with a second's wait before ↓. `=A1+D11`
   (case 8) showed them in 4 of 5.
   - ADR-0057, "What cases 24–32 settled", says the grey "shows for `=SUM(D11`, `=1+D11` and
     `=D11+D12`". Its `=1+D11` is the eighth run's case 30, seen once.
   - ADR-0051, "The Reference being written is shown selected", says the same.
7. **F2 takes Excel's Formula Bar out of Edit** (17, 20–24). After F2 the bar is in Enter. From there,
   Home, → and ↓ entered the Formula into D10 and moved the active cell (20–22). Where a Reference can
   go, ↓ pointed (24). ADR-0051, "An edit in the Formula Bar never enters Overwrite" (on
   `claude/exsheet-eighth-run-b`), says: "Excel's Formula Bar is always in Edit (2026-09-27, item 12,
   "status Edit")."

## Shades observed in group 2

For ADR-0057, "What Part B of the eighth Windows run settled" (`claude/exsheet-eighth-run-b`), and
ticket 43 ("The next Windows run asks Excel for the other five shades"). The text of the Reference
Point is writing, on `#c6c6c6`, in the cell:

- 1: **`#0401a2`** (as the eighth run read it)
- 2: **`#630101`** (as the eighth run read it)
- 3: **`#44007c`**
- 4: **`#003600`**
- 5: **`#550059`**
- 6: **`#531c00`**
- 7: **`#00323f`**

The same seven were read under Office Theme "Black", with the same ground. Under Black the
References' text colours in the cell were also unchanged. The outlines on the grid changed (the table
above).

## Also seen, and asked by nothing

- **The Formula Bar's own colours** while the edit is there (case 18): `#006cbe` and `#bc2f34` for the
  first two References, not the cell's `#326ac7` and `#c0353e`. The eighth run saw the same.
- **The Name Box shows `SUM` while a Formula is edited** (14-b, 18). This is Excel's function list,
  in place of the cell's address.
- **A value tip** (`0`) shows above the edited cell whenever the pointed Reference is shown selected
  (cases 6, 9–13, and the passes of 7 and 8 that showed the grey).

## The machine afterwards

- **The Office Theme reads "Use system setting"** in File › Account, the value found before the run.
  The registry is not byte for byte what it was:
  - `HKCU\Software\Microsoft\Office\16.0\Common` `UI Theme` is `6`. Before the run there was no such
    value.
  - The account's roaming theme setting holds `4` (Black) as its synchronised value and `6` as a
    change waiting to be synchronised. Before the run it was an empty placeholder.

  Office wrote both when the theme was set through its own page. They were left as they are.
- **An Excel of the script's own was left running once.** A theme change back was started at 19:47
  and interrupted. Its Excel (process 3864, started 19:47:57 with `/automation -Embedding`, holding
  only the script's Book1) stayed open, and the theme was not changed.
  - The theme was set back at 19:59 by an Excel of its own, which left 3864 alone.
  - 3864 was then ended with `Stop-Process` at about 20:00, before group 3.
  - Every other Excel of the run quit, or was ended by `Stop-Process` when it had not quit within
    10 s (the Black pass, as in the ninth run). None held a workbook that was not the script's.
- **No Excel process is left.**
- **The AutoRecover workbooks were not touched.** `Book1 (version 1).xlsb` and the unsaved workbook
  in `%APPDATA%\Microsoft\Excel` hash as the backup taken before the first Excel started
  (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run10`).
- **The keyboard** is English (UK), 0x08090809, as the run found it.
- **After group 4 (checked at about 20:25) the same holds.** No Excel process was running before it or
  is left after it: all twelve of its Excels quit by themselves. The AutoRecover workbooks still hash
  as the backup. The keyboard is 0x08090809. `UI Theme` is still `6`, and group 4's pictures show the
  light ribbon, so it ran under "Use system setting" as groups 1–3 did. The environment (case `0`) was
  not read again for group 4.
