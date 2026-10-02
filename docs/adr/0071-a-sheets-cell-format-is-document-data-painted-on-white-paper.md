# A Sheet's Cell Format is document data, painted on white Paper

*(Decided with the user, 2026-09-30, in a design grilling. The question was what to do about cell
formatting beyond the Number Format and the Alignment that ADR-0046 put in the first version.)*

*(Numbered 0063 until 2026-10-01. ExPivot's ADRs hold 0059 to 0070, so this one moved to its
branch's block, as [`docs/agents/numbering.md`](../agents/numbering.md) reserves them. Its tickets
81 to 85 were numbered 76 to 80 for the same reason. The Windows run records in `verification/`
still say ADR-0063.)*

[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md) put
Excel's Number Formats and horizontal Alignment in ExSheet's first version. It left fonts, fills and
borders out until a `spikes/render-bench` mode had measured per-cell styling and an ADR had read the
result. This ADR decides what goes in, how a user applies it, and what it looks like. **Fonts,
fills and borders are in the first version.** How they are painted is still left to that
measurement, which remains the precondition.

Everything recorded about how a cell looks is its **Cell Format**: its Number Format, Alignment,
Font, Fill and Border (`CONTEXT.md`). The code's word for the first two, "style" (`AxisStyle`,
`SheetEdit.SetStyle`), is replaced by it.

## What a Cell Format holds

- **Number Format and Alignment**, as they are today
  ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
- **Font**: a colour, bold, italic, a single underline, and strikethrough.
- **Fill**: one solid colour.
- **Border**: each cell records its own four sides, as Excel's files do. Each side is one of Excel's
  thirteen line styles and has a colour. The styles are hair, thin, medium, thick, double, dotted,
  dashed, dash-dot, dash-dot-dot, medium dashed, medium dash-dot, medium dash-dot-dot and slanted
  dash-dot.
- **A colour is Automatic or an RGB value.** Excel also records theme colours (a theme slot and a
  tint). Those come with `.xlsx` reading, which is where they matter. A colour picked from the theme
  part of a palette is recorded as its RGB value.
- **The new parts follow the rules Number Format and Alignment already follow.**
  - They are recorded at three levels, cell over row over column (SH-21).
  - An inserted row or column takes the Cell Format of the one before it (ADR-0046).
  - An operation is one undo step, over several ranges too.
  - A copy from ExSheet to ExSheet carries them. Ctrl+D, Ctrl+R and the fill handle carry them
    (SH-23), and Delete keeps them (SH-24).
  - They are refused while an edit is open
    ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
- **A colour in a Number Format is now painted.** This covers `[Red]` and the other seven named
  colours. It is painted in Excel's colour for that name, and it wins over the Font colour, as
  Excel's does (a reading until the eleventh Windows run). This replaces ADR-0047's "kept, and not
  yet painted". `[ColorN]` stays refused, because more format codes are a later step.

### What stays out, and why

- **Font size, typeface, wrapped text, vertical alignment and text rotation.**
  - Every row has one height
    ([ADR-0013](./0013-fixed-row-height.md), ADR-0046). Excel grows a row for a larger font or
    wrapped text. A Sheet cannot, so these would cut text.
  - Text of one size in a row of one height has nothing to align vertically.
  - A typeface changes the widths that the `####` decision rests on
    ([ADR-0016](./0016-column-width-and-overflow.md)).
- **Diagonal borders.** They are rare, and they cross the cell where the Focus and the Selection are
  drawn.
- **Rich text**, meaning part of a cell's text in another Font. A Cell Format is per cell.
- **Pattern and gradient fills.**

### Bold, and what fits

`####` and General's fitting charge each character at a width from `CellTextMetrics` (ADR-0016,
ADR-0047). Bold glyphs are wider. At weight 600, a tabular digit measured 9.058px against a
declared 9 (Definition of Done §21.7a). If a bold number were judged by the regular widths, its edge
could be cut, and it would read as a different, valid number. Principle 1 forbids that. **So
`CellTextMetrics` gains bold widths**: each character class measured at the bold weight, supplied
the way the regular widths are. Supplying them is a Wrapper's metrics-bearing obligation
([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)). A bold cell is
judged by the bold widths. The grid still never measures.

Italic is judged by the regular widths. Its slant leans past a glyph's advance by less than the
cell's padding. A layer-3 check confirms that nothing is cut.

*(2026-10-01, ticket 47.)* **The core measured its own bold widths** the way §21.7a measured the
regular ones: Chrome at 14px, tabular digits, weight 700.
- system-ui on macOS measured 14.35 / 9.25 / 5.852 (wide / digit / narrow). DejaVu Sans Bold, which
  Linux paints for both 600 and 700, measured 14.028 / 9.742 / 6.398.
- The defaults take the wider of the two in each class: 14.35 / 9.742 / 6.398.
- `ExGrid.MudBlazor` supplies Roboto's widths at 700: 10.4 / 8.33 / 5.25. *(4.95 at first; ticket 82
  found `/` wider, ADR-0016.)*
- Segoe UI on Windows is not measured.

**A Consumer that supplies metrics without bold widths has bold charged at the regular widths plus
4%.** That is the widest growth from weight 600 to 700 that was measured, on `(` in system-ui on
macOS. Like ADR-0016's fallback for full-width characters, it is reasoned rather than measured for
every font. It errs towards `####`, never towards a cut number.

## Paper and Ink

- **The ground under a Sheet's cells is Excel's white, and Automatic text is Excel's black, in every
  colour scheme.** These are the **Paper** and the **Ink** (`CONTEXT.md`).
  - Excel does the same. Under Office Theme "Black", its cells stayed white, and only the frame
    darkened: the Formula Bar showed white text on `#292929`
    (`verification/2026-09-30-windows-excel-10/excel-only.md`, the tenth Windows run).
  - A colour a user recorded carries a meaning, such as red for a breach or yellow for "check
    this". On white Paper it reads as it did when it was chosen.
- **What lies on the Paper takes its light-scheme appearance.** That is the Focus, the Selection,
  Reference Outlines and the pointed shade, the Cell Editor in its cell, and the gridlines.
  - *(2026-10-01, ticket 48.)* **The gridlines are Excel's:** `#e0e0e0` on white, sampled by the
    eleventh Windows run. They are drawn on every row and column under both Chromes. Before
    ticket 48, the built-in Chrome drew none: the core's rule tokens default to transparent. Under
    MudBlazor only the Wrapper's row rules showed, and they vanished on the Paper in its dark theme.
    - They are the Ink mixed 12% into the Paper, through the core's existing
      `--ex-row-rule-color` and `--ex-column-rule-color`, so a Consumer's Paper and Ink carry them
      along. No new token was added.
    - On the Sheet they replace the Wrapper's row rules. That is the user's criterion, as close to
      Excel as possible.
  - **What frames the Paper follows the colour scheme.** That is the Headings, the Name Box, the
    Formula Bar (with its coloured References), popovers and the Size Tip.
  - The dark-scheme shades of
    [ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) are therefore
    used only in the frame.
- **Both are Visual Tokens, `--ex-sheet-paper` and `--ex-sheet-ink`, with those defaults in every
  scheme.**
  - ExGrid's own defaults follow `Canvas` and `CanvasText`
    ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). A Sheet's Paper
    deliberately does not.
  - ExSheet and `ExGrid.MudBlazor` never map these tokens onto a dark palette.
  - A Consumer may. The colour combinations that then become unreadable are its choice.
- **In forced-colors mode the user's system colours win over the document's**, as they win over
  every other colour of the grid (ADR-0027).

### Considered options

- **Cells follow the scheme, and recorded colours are painted as recorded.** Rejected: some
  combinations become unreadable. Light Automatic text on a recorded pale fill cannot be read, and
  neither can recorded black text on a dark ground.
- **Cells follow the scheme, and recorded colours are converted in the dark scheme.** Rejected:
  the result is readable, but the colour is not the one the user chose. It looks right and is not
  what was recorded.

## How a user applies it

### Keys

- **ExSheet claims Excel's formatting keys**, each only as Excel has it. The eleventh Windows run
  checks each key, and the Number Format each applies under a culture is Excel's, as observed.
  - Ctrl+B or Ctrl+2: bold. Ctrl+I or Ctrl+3: italic. Ctrl+U or Ctrl+4: underline. Ctrl+5:
    strikethrough.
  - Ctrl+Shift with `~`, `!`, `@`, `#`, `$`, `%` or `^`: General, `#,##0.00`, time, date, currency,
    percent and scientific.
  - Ctrl+Shift+`&`: an outline border. Ctrl+Shift+`_`: no borders.
  - Ctrl+1: opens Format Cells (below).
- **A toggle follows the Focus cell.** Over a Selection whose Focus cell is bold, Ctrl+B removes
  bold from every cell. Otherwise it sets bold on every cell. This is a reading until the run.
- **The keys go through ExGrid's key table**
  ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 14). The core claims a key only
  for a Consumer that declares it, and raises it. Without the declaration, the key stays the
  browser's.
- **Two facts are checked before the keys are built.**
  - Does a page receive Ctrl+1 to Ctrl+5 before Chrome's tab switching does? Chromium reserves only
    a few keys, such as Ctrl+T, Ctrl+W and Ctrl+N, and tab selection by number is not among them.
    That is a reading.
  - Does Excel read these keys by character or by key position? This is observed on a Japanese
    layout.
- **While an edit is open, a formatting key changes nothing and says why.** In that state Excel
  formats the selected characters (rich text), which ExSheet does not have.
  - The key is still claimed, so Chrome's Ctrl+U (view source) does not open.
  - The reason is announced through the root's live region and raised to the Consumer, as a
    refused copy is (`OnCopyRefused`).

### The commands

- **`SetCellFormatAsync(CellFormatChange change)`** acts on the Selection.
  - A change names only the parts it sets. Every other part stays as each cell has it.
  - It is one undo step.
  - `SetNumberFormatAsync` and `SetAlignmentAsync` stay, as shorthands for it.
- **Borders in a change are relative to each selected range**, as in Excel's dialog. The options
  are: outline, inside, top, bottom, left, right, inside horizontal, inside vertical, and none.
  Over several ranges, each range gets its own outline, as in Excel.
- **`CellFormatAt(address)` answers a cell's Cell Format as it shows**, cell over row over column.
  - A toolbar reads it for the Focus cell whenever the Selection changes.
  - Format Cells reads it when it opens.
- **`OpenFormatCellsAsync()`** opens Format Cells from the Consumer's own button.
- **The commands that change the Sheet, and `OpenFormatCellsAsync`, are refused while an edit is
  open** (ADR-0048, SH-29). `CellFormatAt` is a read, and it answers in any state. *(Clarified
  2026-10-01 by ticket 50. The bullet first said "all of them", which would have taken in the
  read.)*

### Format Cells, a Chrome seam whose frame the Chrome chooses

- **Three things open it**: Ctrl+1, `OpenFormatCellsAsync`, and "Format Cells…" in the Context Menu.
  The menu item is a Consumer command
  ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)), as ExSheet's
  insert and delete commands are.
- **ExSheet decides what it offers**
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). The Chrome draws it and calls back
  with a `CellFormatChange`.
  - **Tabs**: five of Excel's six, namely Number, Alignment, Font, Border and Fill. Protection is
    left out.
  - **Number**: Excel's categories in Excel's order.
    - A category whose codes ExSheet does not read is shown disabled, with the reason. These are
      Accounting, which pads with `*`, and Fraction.
    - Custom takes a code, and a code ExSheet does not read is refused by name.
  - **Alignment**: horizontal only.
  - **Palette**: Excel's default. That is the theme colours in six tints, ten standard colours, and
    More Colours. The values are the current Excel's, as the eleventh run reads them. Excel's
    default theme changed with Aptos.
  - **Border**: the thirteen line styles.
- **OK applies only what the user touched**, as one undo step. A part the user left alone keeps
  each cell's own value.
  - The dialog opens on the Focus cell's Cell Format.
  - How it shows a part that differs across the Selection is Excel's, as observed.
- **This is the first seam whose frame belongs to the Chrome.** Everywhere else, the core owns where
  a seam appears and how it opens and closes (`CONTEXT.md`, Chrome).
  - **The built-in Chrome** shows Format Cells as a popover inside the Sheet's box
    ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)). It scrolls when the box is small.
    More Colours takes a hex value. No script is added.
  - **`ExSheet.MudBlazor`'s Chrome** shows it as a `MudDialog`, a page-level modal that MudBlazor
    already draws. More Colours is MudBlazor's colour picker. We add no script
    ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).
    - While the dialog is open, keys go to it. It lies outside the Sheet's root, so the Sheet's
      listener does not hear them
      ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).
    - On closing, the Chrome calls the core's focus function to return the keyboard, as a Chrome's
      editor does (ADR-0021's note of 2026-09-30).
    - *(2026-10-01, ticket 53.)* On closing, a `MudDialog` hands focus back to whatever held it at
      opening. When a page's button opened it, that is the button, and the core then declines the
      hand-back, because the keyboard is somewhere else.
      - So before opening, the Chrome moves focus to an empty element of its own, through Blazor's
        `FocusAsync`, and removes that element once the dialog has the keyboard. On closing, focus
        lands on nothing, and the core takes the keyboard back.
      - No script is involved. A Chrome that owns its frame is responsible for leaving the keyboard
        where the core will take it back.
      - The hand-back waits until the browser has drawn the dialog's removal: the frame closes the
        dialog, re-renders, and calls the core's focus function from its `OnAfterRenderAsync`. On a
        circuit, an earlier call reached the browser before the removal and was declined, which
        left the keyboard on `body` (found on the Server host by ticket 53's follow-up).
      - A dropdown in the dialog keeps Escape while its list is open, so the first Escape closes only
        the list, as in Excel.
  - **Tabs switch as in ARIA's tabs pattern**, with the arrow keys on the tab list. Excel's Ctrl+Tab
    and Ctrl+PageDown are reserved by Chrome for its own tabs, so a page never receives them.
- **A Consumer with its own dialog substitutes its own Chrome**, as with the filter panel
  ([ADR-0009](./0009-filter-panel-contract.md)).

### Considered options (the frame)

- **A popover inside the Sheet's box under every Chrome.** Rejected for the MudBlazor Chrome.
  MudBlazor already has a page-level dialog that needs no script of ours, and a Sheet in a small box
  would get a cramped dialog for no gain.
- **A page-level modal under every Chrome.** Rejected for the built-in Chrome. Without a design
  system, a modal needs `<dialog>.showModal()`, which would be a new entry on ADR-0021's allowlist.

### A toolbar ships as an opt-in part of ExSheet *(replaced 2026-10-02)*

*Until 2026-10-02 this section said "A toolbar is a sample, not a product": a formatting toolbar,
Excel's ribbon, was to be built on the DemoHost from `SetCellFormatAsync`, `CellFormatAt` and the
notification that an edit has opened or ended, and not shipped, because "a toolbar is buttons that
call public commands. Shipping one would promise its look and the order of its buttons."*

The reason was answered rather than overruled. The look is the Chrome's, as every seam's is, and the
order is the Consumer's to declare, so neither is promised. What is promised is which commands exist
and what each means. The Sheet Toolbar is decided in
[ADR-0100](./0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md). It is still built only on
the public commands above.

### A package for the MudBlazor Chrome

**`ExSheet.MudBlazor`** references `ExSheet`, `ExGrid.MudBlazor` and MudBlazor
([ADR-0019](./0019-one-repository-many-packages.md)).
- Cell Format is ExSheet's concept. If its Chrome went into `ExGrid.MudBlazor`, every ExGrid-only
  Consumer on MudBlazor would pull in ExSheet.
- The package stays outside the release, as ExSheet does (Definition of Done §2). The package smoke
  check packs it.

## How it is painted: measured first

- **ADR-0046's precondition stands.** A `spikes/render-bench` mode measures per-cell appearance
  before the painting is chosen.
  - **Candidates for Fill and Font**: an inline `style` on each cell, a per-cell custom property
    read by a static rule, or interned classes, one per distinct Cell Format, in a generated
    stylesheet.
  - **Candidates for Border**: a layer over the rows, as the selection overlay is
    ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)), or lines inside each cell's box.
- **Borders look like Excel's.**
  - A line is centred on the gridline, and a thick line reaches into both cells.
  - Lines are drawn above Fills, and below the Focus and the Selection.
  - A layer is the likely way to do this. If the measurement shows the layer is too costly, the
    choice comes back to the user: lines inside each cell's box sit 1–2px away from Excel's.
    *(Both predictions were wrong. The layer was the costliest mode, and a mode inside each cell
    matched Excel's pixels, so nothing came back. See "What the measurement chose", below.)*
- **A line recorded on both sides of the same edge** is drawn by Excel's rule, as observed.
- **A Fill covers the gridlines at the cell's edges**, as Excel's does. This is a reading.
- **Whatever mechanism is chosen, P1–P9 hold** (ADR-0027): rows skip their render, nothing per
  cell reaches JavaScript, and the DOM does not grow.
- **The core's hook is opt-in** (ADR-0050, item 15).
- The measurement and the choice are recorded here as an addendum.

### What the measurement chose *(2026-10-01)*

Ticket 44 measured the candidates. Its comment holds the tables. The result files are
`spikes/render-bench/results/20261001-113322-829.json` and `-133041-335.json` for Font and Fill,
`-153002-709.json` and `-160808-773.json` for Borders, and `20261001-geometry/` for the pixels.
- All of it ran in headless Chrome 154 on an Apple M4 Pro, so the deltas against a baseline measured
  in the same sweep are what count.
- A fling frame paints 40 new rows of 20 cells. The baseline frame was 13 to 15 ms.
- The user's standing criterion (2026-10-01) decides between the modes: as close to Excel as
  possible, and rows keep one height. Between modes equally close to Excel, the cheaper one is
  taken.

**Font and Fill: interned classes.**
- Each distinct Font and Fill gets one rule in a generated stylesheet, and a cell names its class.
- Every candidate paints the same pixels, so cost decides. Classes were the cheapest at every share
  and every number of distinct formats, in both runs: +0.6 to +1.5 ms at the median.
- Their tail was the tightest: a maximum of 17.5 ms, where an inline `style` reached 26.9 and a
  per-cell custom property 23.5.
- A new format rewrites the stylesheet. That cost the same as the other modes, within 0.5 ms.

**Borders: inside each cell, each cell painting its own share of Excel's centred line.**
- Each edge is resolved once per row, outside the render, from both cells' records. The Consumer
  answers which line wins (ADR-0050, item 15). The cell then names interned classes, one per side,
  line style and colour, and draws its share as background layers.
- **It is the only mode that matched Excel's pixels for all thirteen line styles at 100%**:
  - thin on the gridline;
  - medium on the gridline and the pixel above it;
  - thick on the gridline and a pixel either side;
  - double as two lines either side of a white gridline;
  - the dash patterns, though their phase differs from Excel's.
- It changes no row's height, moves no text, and paints nothing outside its row, so the rows stay
  the boundary that skips renders (P1–P9 held in every configuration).
- **Its cost grows with the number of distinct lines on screen.** That is style recalculation and
  the rasterising of gradients, not the number of elements:
  - +0.4 to +2.3 ms a frame with 1 to 16 distinct lines, which covers how sheets are made;
  - +4.6 to +5.0 ms (a median of 18.2 to 19.2 ms) when half or all of the visible cells each carry
    one of 256 random lines, which is a sheet nobody makes.
  - The geometry is the product's claim, so the cost is taken. Performance never gates (AGENTS.md).
- **The layer over the rows is rejected.** It cost +2.5 to +12.6 ms a frame, and up to 1600
  elements for 800 cells, because every line moves with the painted slice. Splitting it into one
  strip per row cut the slow-scroll cost but not the fling's.
- **Lines inside each cell's box, without the shares, are rejected.** They cost +0.1 to +1.1 ms, but
  thick and double sit a pixel high, and a wider right border moves the text.

**Still owed, and where it is owed.**
- **150% is not yet settled.**
  - Under CDP's device-scale emulation, the chosen mode drew its 1-px parts and its dashes in
    place. A 2-px part (medium, and the upper part of thick and double) came out at about 1.5 device
    pixels, with an antialiased row.
  - A real browser zoomed to 150%, and Windows, were not tried.
  - Ticket 49's pixel tests at 100% and 150% settle it (DC-59). If Excel's pixels cannot be had at
    150%, that is the trade that goes back to the user.
- **Ticket 47 measures two more things in `spikes/render-bench`.**
  - A hybrid that keeps the geometry: solid lines as the cell's own `border`, and background layers
    only for dashes, double and the pixel past the gridline. Interning stays per side, style and
    colour, never per combination of four sides; the variant that interned combinations grew its
    stylesheet with every one.
  - A Fill and a border on the same cell.

  The hybrid is taken if it draws the same pixels and costs less.
- **A run on real hardware and one on the Server host** are owed by hand. Neither gates.

**What ticket 47 settled** *(2026-10-01)*. The result files are `results/20261001-183929-354.json`,
`-190312-851.json` and `-192214-388.json`.
- **The product uses the hybrid.**
  - A solid line on the gridline is the cell's own bottom or right `border`. A right border gives
    its width back out of the padding, so the text does not move.
  - Dashes, double, the pixel past the gridline, and a neighbour's Fill over the gridline are
    background layers, read by one static rule and interned per side, style and colour.
  - It draws the same pixels as `BorderInCellExcel`, and costs 0.2 to 0.8 ms less at the median in
    five of six configurations, and 0.2 to 0.4 ms more at 50% with 16 distinct lines. The first of
    the three sweeps ran at a load of up to 25 and is not counted.
- **150% is exact** under a real device scale, with Chrome's `--force-device-scale-factor=1.5`:
  thin is 1 device pixel, medium 2, thick 3, double dark / ground / dark, and the long dash 9. DC-59
  passes there on both hosts.
  - CDP's emulation, which the earlier measurement used, blurs borders and 1-px tiles at 1.5. No
    user has that display.
  - Windows and a browser zoomed to 150% are still owed, in the eleventh run's Part C. *(Answered
    2026-10-02: "What Part C of the eleventh Windows run found", below. Lines are exact at a display
    scale of 150%; a vertical gridline, and every line at a browser zoom between the steps, are not.)*
- **A Fill and a border on the same cell** cost +0.1 to +0.2 ms over the border alone with one Fill
  colour, +1.5 to +2.6 with 16, and +2.6 to +5.6 with 256 Fills over 256 lines.
- **A Fill covers all four of its gridlines, beneath any line.**

## Not in it

- **The Font and Border exclusions above**: font size, typeface, wrapped text, vertical alignment,
  rotation, diagonal borders and rich text.
- **Fills and colours**: pattern and gradient fills, and theme colours. Theme colours come with
  `.xlsx`.
- **More Number Format codes**: `[ColorN]`, conditions, `*` padding and fractions.
- **Conditional formatting**, named Cell Styles, Paste Special and the Format Painter.
- **Cell Format across the clipboard beyond today's.** A paste from elsewhere reads no styles.
  `mso-number-format` is still not read. A copy outward still writes only the Number Format
  (ADR-0048).

## Readings until the eleventh Windows run

`docs/specs/exsheet/verify-on-windows-11.md` asks Excel about each of these. Each is corrected here
when the answer comes.

*(Answered by the eleventh Windows run, 2026-10-01. The next section says what each answer settled.)*

- Does a Number Format's colour win over the Font colour? Which RGB is each of the eight names?
- Where no section of the Number Format shows the Value (General, text in a format without a text
  section, a boolean, an Error Value), is there no colour? Does a `####` keep its section's colour?
  *(Readings added 2026-10-01, from ticket 46.)*
- Does a Fill hide the gridlines at the cell's edges?
- When both sides of an edge are recorded, which line is drawn, and does a later operation also
  write the neighbour's side?
- Is a toggle key's direction decided by the Focus cell?
- The formats each Ctrl+Shift key applies, under the Sheet cultures ExSheet supports.
- Are the keys read by character or by position on a Japanese layout?
- Excel's default palette.
- How Format Cells shows a part that differs across the Selection.
- What an inserted row takes from a bordered row above.
- An outline over whole columns. Ticket 45 reads it as left and right recorded at column level, and
  the top of row 1 and the bottom of row 1048576 recorded on those cells, because a level is
  uniform along its length. Whole rows mirror this.
- Does a page receive Ctrl+1 to Ctrl+5 in Chrome and in Edge? This is part B, in the browser.

## What the eleventh Windows run settled *(2026-10-01)*

The run was made at 76d3866, with Microsoft 365 and the Office theme. Its records are
`verification/2026-10-01-windows-excel-11/cell-format.md` and
`verification/2026-10-01-windows-browser-11/keys.md`. Each answer below is Excel's, and ADR-0047's
rule applies: a disagreement is fixed to Excel's answer. None of them needed a new decision.

- **These readings held**, and are no longer readings:
  - A Number Format's colour wins over the Font colour. The eight names are the legacy palette.
  - No section that shows the Value means no colour, and a `####` keeps its section's colour.
  - A Fill covers the gridlines at its cell's edges.
  - A thick line is centred, takes a pixel into each cell, and lies above Fills and below the
    Selection.
  - Each range gets its own outline.
  - The toggles follow the Focus cell.
  - Format Cells' OK sets only what was touched.
  - A page receives Ctrl+1 to Ctrl+5, Ctrl+B, Ctrl+I and Ctrl+U in Chrome and Edge before the
    browser, and never receives Ctrl+Tab or Ctrl+PageDown.
- **The line between two cells is one line** (cases 7 and 13).
  - Setting B2's right edge makes C2's left edge read the same, and the later setting wins, from
    either side.
  - Clearing a range's borders clears the same edges read from the cells outside it.
  - So ExSheet writes both cells' sides whenever it sets or clears an edge. Each cell still records
    its own four sides (`CONTEXT.md`, Border), and the two records never disagree. Ticket 45's
    reading ("the cell's own side only") is corrected by ticket 55.
- **An inserted row takes the Fill of the row above, and not its Borders** (case 12). The new row's
  top edge is the edge it shares with the row above, so it reads that row's bottom line. Nothing
  else was repeated. Ticket 45 copied Borders, and ticket 55 corrects it. The Font is read as
  copied, like the Fill; that is not yet observed.
- **An outline over whole columns sets only their left and right edges** (case 15). Neither the top
  of row 1 nor the bottom of row 1048576 is set. Whole rows are read as the mirror: top and bottom
  only. Ticket 45's reading differed, and ticket 55 corrects it.
- **The formatting keys are read by the character they type, not by key position** (case 19).
  - On a UK layout, `#` needs no Shift, and Ctrl+`#` applies the date format.
  - So ExSheet matches the key's character (`KeyboardEvent.key`) with Ctrl, and whichever Shift the
    layout needs.
  - The run's Japanese layout ran over the 101-key arrangement, so it could not tell the two ways
    apart.
- **What the keys apply** (cases 18 and 20). Under en-GB, en-US and ja-JP:
  - `~`: General. `!`: `#,##0.00`. `%`: `0%`. `^`: `0.00E+00`. `#`: `d-mmm-yy`, the same in all
    three.
  - `@`: `h:mm`, and `h:mm AM/PM` under en-US.
  - `$`: Excel's built-in currency format, localised by the culture. Under en-GB it is
    `£#,##0.00;[Red]-£#,##0.00`. Under ja-JP it is `¥#,##0;[Red]-¥#,##0`, with no decimals.
    Under en-US it is `$#,##0.00_);[Red]($#,##0.00)`.
  - ExSheet records the currency key's format as a culture-localised built-in, as it already records
    Excel's built-in short date.
  - *(Built by ticket 51.)* The local form comes from .NET's currency data, with one correction:
    under en-US, Excel follows Windows and puts a negative amount in parentheses, where .NET writes
    `-$n`. Cultures other than en-GB, en-US and ja-JP follow .NET's data. They have not been checked
    against Excel.
- **Format Cells** (cases 22, 24 and 26).
  - The tabs are in Excel's order. A fresh Excel opens on Number, and Ctrl+1 then reopens on the
    last tab shown.
  - The Border tab lists its line styles in two columns. It offers None, Outline and Inside, and
    Inside is disabled for one cell.
  - Parts that differ across the Selection are shown as Excel shows them. The Font style is empty, a
    differing Fill shows No Colour, and a differing inside edge is a grey dotted line.
  - Ticket 52 takes these.
- **The palette** (case 23) is the Office theme's 60 colours and the 10 standard colours.
  - A swatch is recorded as the RGB value Excel names it with in the Fill tab, not as the colour
    sampled from the screen.
  - 38 of the 50 tints are drawn 1–3 away from their named value. That is Excel's rendering, and
    recording the named value is what Excel would report.

### Seen by ticket 51 in the run's record, and not yet built

- **Excel widened column A for the date key** (case 18). ExSheet widens a column when something is
  entered (ADR-0047), never when a format changes. Whether a formatting key widens a column at the
  default width needs an observation of its own.
- **Under en-GB, `NumberFormatLocal` reads `hh:mm` for the time key and `dd-mmm-yy` for the date
  key.** That suggests Excel localises built-ins 20 and 15 as it localises 16. The run's samples
  (`12:00`, `18-May-03`) cannot tell. ExSheet shows them as their codes spell them.

### Where ExSheet stays unlike Excel *(follows from decisions above)*

- **Excel raises a row's height for a medium or a thick line** (cases 8 and 9). A Sheet's rows keep
  their one height (ADR-0046), so the line takes its pixels from the rows it lies across.
- **Ctrl+1 during an edit opens a Format Cells with a Font tab alone in Excel**, for the selected
  characters (case 21). ExSheet has no rich text. Ctrl+1 changes nothing there and says why, as the
  other formatting keys do.
- **Excel en-GB spells the numbered colour `[Colour10]`**, and refuses `[Color10]` (case 3).
  `[ColorN]` is still refused here. When numbered colours come, the spelling is part of that step.

## Readings until the twelfth Windows run *(2026-10-01)*

`docs/specs/exsheet/verify-on-windows-12.md` asks Excel about each of these, and each is corrected
here when the answer comes.
*(Answered by the twelfth Windows run, 2026-10-01. The next section says what the answers settled.)* They are readings, not decisions: the answer is Excel's (ADR-0047's
rule). Where Excel has not answered, they say what ExSheet does until it has.

- **A paste, Ctrl+D, Ctrl+R and the fill handle keep the line between two cells one line.** Each
  writes the neighbour's side as well, and the later write wins, as setting an edge does (run 11,
  case 7). Today they write each cell's own sides only, so the two records of an edge can disagree.
  Ticket 57 changes that once the run has answered.
- **When deleting rows or columns brings two edges together, the upper row's line wins (the left
  column's, for columns).** That is the row that did not move. The core asks the Consumer which line
  to draw (ADR-0050, item 15), so ExSheet answers by this rule until the run answers.
- **Insertion** (ticket 55's readings):
  - A row inserted at row 1 takes nothing, and the Sheet's top line goes.
  - Of several inserted rows, only the first one's top reads the line above.
  - An inserted row takes the Font as it takes the Fill.
  - An inserted column mirrors an inserted row.
- **Outlines**: one over whole rows sets only top and bottom. One over the whole Sheet sets only the
  left of column A and the right of column XFD. An inside line over whole columns also shows on the
  top of row 1 and the bottom of row 1048576.
- **The two items ticket 51 left** (above): whether a formatting key widens a standard-width
  column, and whether Excel localises built-ins 15 and 20.

## What the twelfth Windows run settled *(2026-10-01)*

The run was made at a0ed1e6. Its record is
`verification/2026-10-01-windows-excel-12/cell-format-12.md`. Every answer is Excel's (ADR-0047's
rule), and none of them needed a new decision.

**How Excel keeps the line between two cells.** All of the eleventh and twelfth runs' answers about
edges follow from one model, and ExSheet takes it in place of ticket 55's (which wrote both sides on
every setting).
- **Each cell records its own four sides**, as Excel's files do.
- **Setting or clearing an edge** with a border command (a key, Format Cells, `SetCellFormatAsync`)
  records it on the cells it was set on, and **clears the neighbour's record of the same edge**.
  That is why the later setting wins from either side (run 11, cases 7 and 13).
- **Copying a Cell Format** (a paste, Ctrl+D, Ctrl+R, the fill handle) writes each target cell's
  own four sides, and touches no neighbour (cases 1–5).
- **Where both cells record a line on one edge, the upper cell's is the one shown, and the left
  cell's for a vertical edge. Where only one records a line, that line is shown, from either
  side.** This covers:
  - a paste over a cell whose neighbour had a line (case 1);
  - the edge a Ctrl+D source shares with its first target, which keeps the source's bottom although
    the source has no top (case 3);
  - two rows brought together by a deletion: the upper row's line, or the lower's when the upper
    has none (cases 6, 7 and 9).
- **Inserting and deleting move the cells with their own records.** An inserted row takes no
  Borders.
  - A line on row 1's top therefore moves down with row 1, and shows between the new row 1 and
    row 2 (case 10). This corrects ticket 55's reading.
  - The edge between a row and the row inserted under it is the row's own bottom, so it still
    shows (cases 11 and 12 of the eleventh run).

**What else the run settled.**
- **An outline over a whole row** sets its top and bottom, and the left of column A, but not the
  right of column XFD (case 14). Excel draws nothing at A's left beyond the Row Headings' edge.
- **An outline over the whole Sheet sets nothing** (case 15).
- **An inside line over whole columns** also shows on the top of row 1 and the bottom of row
  1048576 (case 16), as ticket 55 read it.
- **An inserted row takes the Font as it takes the Fill**, and an inserted column mirrors an
  inserted row (cases 12 and 13).
- **Every formatting key widens a column at the standard width whose formatted text no longer
  fits**, and the column then leaves the standard width (case 17). A column the user has sized is
  never widened (case 18). Format Cells and `SetCellFormatAsync` are read as doing the same, which is
  not yet observed.
- **Excel localises the date key's and the time key's built-ins** (case 19).
  - Under en-GB they show `05-Jan-26` and `09:05`.
  - Under en-US they show `5-Jan-26` and `9:05 AM`; the time key writes the AM/PM built-in.
  - Under ja-JP they show `05-1-26` and `9:05`; the month is a number there.
  - ExSheet records them as culture-localised built-ins, as it records the currency key's.
- **Excel raises a row for a thick line** in these cases too. ExSheet's rows keep one height
  (above).

Ticket 57 takes the model. Ticket 58 takes the keys' widening and the localised built-ins.

**The Selection's outline over Borders** *(2026-10-01, decided with the user, ticket 49).* Excel's outline
covers a range's outer lines on all four sides (case 11). ADR-0008's note of this date moves ours out to
Excel's place, except beside the Headings, a Pinned Column and the header.

## Readings until the fourteenth Windows run *(2026-10-01)*

Ticket 58 built the widening and the localised built-ins, and ticket 57 built the edge model. Building
them meant reading some things that no run observed, and one earlier answer may contradict them. ExSheet follows each reading below
until [`verify-on-windows-14.md`](../specs/exsheet/verify-on-windows-14.md) observes it. Excel's answer
then decides (ADR-0047's rule).
*(Answered by the fourteenth Windows run, 2026-10-02. The section after this one says what the answers
settled.)*

- **A column that has already left the standard width widens again** when a key's text no longer fits,
  provided an entry or an earlier key widened it. A column the user sized never widens, as for
  entries (SH-26).
  - **The eleventh run's case 20 may contradict this.** Under en-US, `$` showed `########` at what the
    record calls the standard width.
  - That case pressed the keys on one cell in turn, so the date key had probably widened column A to
    8.73 first. If so, `$1,234.50` with its `_)` padding did not fit 8.73, and Excel did not widen
    again.
- **What widens.**
  - Only a Number Format widens.
  - General widens as typing the number would.
  - A Font, a Fill or a Border never widens, and a bold number that no longer fits shows `####`.
- **Format Cells' OK and `SetCellFormatAsync` widen as a key does.** Two cases in the corpus (FMT-052
  and FMT-072) set the format through COM after the number was typed, and Excel widened the column.
  The dialog itself is not observed.
- **Under ja-JP only built-in 15 shows the month as a number.** Excel's own local code for 15 reads
  `dd-mmm-yy`, yet it shows `05-1-26`. So Excel may show `mmm` as a number in every format under ja-JP.
  ExSheet shows .NET's `1月` there.
- **Ticket 57's model, where the runs saw only the edge as shown:**
  - **Format Cells shows each cell's own sides, not the edge as shown.** The eleventh run's case 24
    observed this for the inside edge of A1:A2. ExSheet also reads it for a single cell: A2 opens with
    no top under A1's thick bottom.
  - **A paste leaves the neighbour's own record under the line that is shown.** In case 12-1, C2 keeps
    its thin blue left under B2's thick red right. COM and the pixels show only the edge, but the
    saved file shows each cell's own record.
  - **Inside over whole rows** sets the right of XFD and leaves A's left alone. **Inside over the
    whole Sheet** sets every side, including A's left and XFD's right. Both follow from cases 14
    to 16.
- **Ticket 47's painting, where the eleventh run's case 9 saw one cell at a time:**
  - Where two filled cells meet, the upper or left cell's Fill shows on the gridline between them.
  - A double line's middle pixel is the grid's ground, even over a Fill.
  - The long dash is 8 pixels below 150% and 9 from 150% on. The runs saw only 100% and 150%.
- **Tickets 88 and 89, where no run looked:**
  - **The Cell Editor takes the edited cell's Fill and Font.** That covers italic, underline and
    strikethrough. A Number Format's colour is left out, because the editor shows the Entry.
  - **The editor covers the cell's own bottom and right line shares while it is open.**
  - **`0;[Red]@`**: Excel kept it as written and showed 5 in black (case 3b).
    - -5 and 0 show in the first section, with no colour.
    - Text shows in the last section's red.
    - In three sections that end in `@`, zero takes the first section.
- **Not a reading but an estimate.** ExSheet widens column A for `05-Jan-26`, which fitted Excel's 8.09
  in case 19. ExSheet's fitting charges every character one digit width (ADR-0047). That is the same
  estimate that otherwise decides `####`, and it errs towards widening, never towards hiding text.

## What the fourteenth Windows run settled *(2026-10-02)*

The run was made at 523c0b9. Its record is
`verification/2026-10-01-windows-excel-14/cell-format-14.md`. Every answer is Excel's (ADR-0047's
rule), and none of them needed a new decision. A reading above that the run confirmed stands as
written. This section says what the run changed, and what it still left to a reading.

**Widening.**
- **A column that has left the standard width widens again** (cases 1 and 2), as read. Under en-US
  the date key widened column A to 8.73 and the currency key widened it again, to 8.91 (case 3). The
  eleventh run's `########` was not reproduced.
- **Ctrl+5 widens as a Number Format key does** (case 7). Ctrl+B and the Fill do not, in any of three
  orders. This corrects "a Font never widens" for strikethrough.
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks.
  - `SetCellFormatAsync` setting strikethrough is read as the Font tab, and does not widen. It sets
    a Font as the tab does (ticket 100).
- General widens as typing the number would (case 5), and a bold number that fits stays as it is
  (case 6), as read.
- A Number Format set through COM on a cell that already held the number widened the column (case 7,
  pass a), as in FMT-052 and FMT-072. `SetCellFormatAsync` and Format Cells' OK go on widening as a key
  does. Format Cells' Number tab widened as read (case 8), and its Border tab did not (case 9).

**`mmm` under ja-JP** (cases 10 and 11).
- **`mmm` shows the month as a number, with no leading zero, in every code**: `05-1-26`, `1 5, 2026`,
  `2026/1/05`. `mmmm` shows `1月`. These are Windows' month names under ja-JP; .NET's `1月` for `mmm` is
  ICU's, as `Sept` was for en-GB. ExSheet shows Windows' names.
- **A code typed into Format Cells is a built-in only when it spells that built-in's code under the
  Sheet's culture.** Under ja-JP, `dd-mmm-yy` is built-in 15 and shows `05-1-26`. `d-mmm-yy` is a code
  of its own and shows `5-1-26`.
  - **How the Sheet Document keeps the difference** *(ticket 103)*. It stores a Number Format as its
    invariant code, and an invariant code that spells a built-in means that built-in. A code of its
    own that spells one is stored with its separators escaped (`d\-mmm\-yy`). Excel reads the escaped
    code as the same code, and writes such codes itself. Format Cells shows it as the user typed it.
    The other way was a field that marks a code as its own, at a new document version. It keeps
    nothing more of Excel's, so the version stays.
  - The same rule holds under every culture. Under en-GB, `d-mmm-yy` typed into Format Cells shows
    `5-Jan-26`, not built-in 15's `05-Jan-26`. No run has typed it there.
  - Format Cells' Custom list and its code box spell each code in the Sheet's culture, so a Custom OK
    never turns a built-in into a code of its own unasked.

**Ticket 57's model, read from the saved files** (cases 12 to 15).
- **Each cell records its own sides**, as read. After a paste, C2 still records its thin blue left
  under B2's thick red right (case 12).
- **Format Cells shows a range's outer edge as it is drawn.** Where the cell records no line, a
  neighbour's line on that edge shows. Under A1's thick bottom, A2 opens with a thick top, its button
  pressed (case 13), though A2 records nothing. An inside edge shows mixed where the two cells' own
  records differ (the eleventh run's case 24).
  - OK still sets only what the user touched (SH-45). Taking the shown top away clears that edge on
    both sides, as clearing an edge does. That is a reading; no run pressed it.
  - Where both cells record a line on an outer edge, the dialog shows the one drawn, the upper or
    left cell's (ticket 102). No run looked.
- **Inside over whole rows sets every vertical side, A's left and XFD's right included**, and the
  lines between the rows (case 14). Excel records it as a row format: row 3 holds left, right and
  bottom, and row 4 left, right and top.
- **Inside over the whole Sheet sets every side** (case 15), as read. Excel records it as one format
  over every column.

**Ticket 47's painting** (cases 16 to 18).
- **Where two filled cells meet, the gridline between them takes the lower cell's Fill** (case 16).
  Where only one cell is filled, its Fill covers that gridline, on all four sides, as before.
  - Between two filled cells side by side, the right cell's is read. That is the cell Excel would paint
    later, as it painted the lower. No run looked.
- **A double line's middle pixel shows the Fill** (case 17), as the gridline beneath it would. Where
  neither cell is filled, it is the ground, as the eleventh run saw.
- **The long dash is 9 device pixels at every scale** (case 18). Excel drew 9 on and 3 off, and thin
  dashed 3 on and 1 off, at every zoom from 50% to 200% on a 150% display. Those zooms span 0.75 to
  1.35 device pixels to a pixel at 100%. So a line's pattern is in device pixels, and does not grow
  with the scale.

**A cell while it is edited** (cases 19 to 21), as read.
- The Cell Editor shows the cell's Fill and Font, and the outline lies over the cell's lines.
- `0;[Red]-0` with a blue Font is blue while edited.
- Under `0;[Red]@`, numbers show black and text red.

### Seen in the run, and left to the next PR

- **Excel drops the Selection outline's inner white ring while an edit is open** (case 19). That
  belongs to the outline, not to a Cell Format.
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab.

Tickets 99 to 103 build these answers.

## What Part C of the eleventh Windows run found *(2026-10-02)*

The run was made at c09c759, which is 523c0b9 with the procedure added. Its record is
`verification/2026-10-02-windows-excel-11c/beside-excel.md`. It put ExSheet beside Part A's Excel, case
by case, under Chrome and Edge, on both hosts, at a display scale of 150%.

**What reads as Excel.** 16 of the 23 cases, and the browsers and hosts agree. Under the dark scheme the
Paper and everything on it read pixel for pixel as under the light scheme (SH-39).

**What was already answered.**
- **Deliberate, as recorded above.** Rows keep one height (cases 8, 9 and 12x). Format Cells has no
  Protection tab and no diagonal edges (case 22).
- **The long dash is 9 at 100%** (case 9). The fourteenth run answered it, and ticket 99 built it.
- **Format Cells over C2 showed C2's own left**, not B2's line on that edge. The fourteenth run's case 13
  answered it, and ticket 102 built it.
- **Escape with no edit open sent the keyboard to the page.** ADR-0012's rewrite of 2026-10-01 (PR #43)
  answered it; the run's commit was older.

**What is owed: device pixels at every scale.**
- **At a display scale of 150% and a browser zoom of 100%** (`devicePixelRatio` 1.5), every line reads
  as Excel's.
- **A vertical gridline does not.** It is one CSS pixel over a column edge that lies on half a device
  pixel (a column is 99 CSS px), so it shows as two device pixels, `#F0F0F0` and `#E0E0E0`. Excel's is one
  pixel of `#E0E0E0`. A horizontal gridline is one pixel, because rows lie on device pixels (ADR-0053).
  - *(Ticket 99's follow-up, 2026-10-02.)* A horizontal gridline was one pixel only where Chrome
    rasterises on the GPU. Its software rasteriser, which CI's Chrome uses under xvfb, drew the 1 CSS px
    band two device pixels deep at 150%. A row's rule, and a Fill's cover over it, are now painted in
    whole device pixels (`--ex-rule-dp`, the rule's width rounded down to the device pixel). That is one
    at 150% under either rasteriser, and unchanged at whole scales. It holds for every grid, not only a
    Sheet. A column's rule is left as it is, with the rest of this item.
- **At a browser zoom of 150% on that display** (`devicePixelRatio` 2.25), the device pixel is taken from
  the nearest step below, 2. So:
  - a gridline is two device pixels, and thin covers one of them;
  - thick is not centred on the gridline;
  - the dashes grow to 10 or 11.
  - The same holds at any resolution between two steps.
- **Exactness at every scale needs two things this ADR does not decide.** Column edges must lie on
  device pixels, and the device pixel must be known at every resolution. The second is either many
  `resolution` steps or a script that reports `devicePixelRatio`, which ADR-0021 would have to admit. The
  next PR takes it.

**Left to the next PR.** These are new, or belong to something other than a Cell Format.
- **Ctrl+Shift+= inserts no row** (case 12). Excel inserts one. ExSheet does not claim the key, so the
  browser zooms the page (ADR-0050, item 14). Claiming it takes the browser's zoom key, which is a
  decision. The Context Menu's insert reads as Excel (case 12x).
- **`####` is six `#` where Excel shows nine** at the same column width (case 3c).
- **The Selection over lines** (case 11).
  - Excel's outline is 2 device px of `#217346` with a white line inside. ExSheet's is 2 CSS px in the
    Ink, or in the palette's primary under `ExSheet.MudBlazor`.
  - Excel's shade is `#C7C7C7`, and its lines lie above the shade. ExSheet's lines lie below the
    Selection (DC-59), so its shade tints them.
  - Matching it is a decision about DC-59 and the Selection's tokens on the Paper.
- **Format Cells' Font tab has no *Normal font* box** (case 24).
- Seen beside the readings:
  - the Row Headings' edge is `#C6C6C6` (Excel: `#ABABAB`);
  - `#DIV/0!` has no error triangle;
  - ExSheet's cells are larger than Excel's;
  - `ExSheet.MudBlazor`'s Format Cells hides Font and Fill behind its scrolling tab strip when it opens
    on Border.

## What was decided after Part C *(2026-10-02, decided with the user)*

Each item Part C left to the next PR, and the two the fourteenth run left (above), is decided here or
in the ADR named. Tickets 140 to 147 build them.

**Device Pixels at every scale:
[ADR-0090](./0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md).** The browser
tells the grid its Device Pixel, and column edges are put on Device Pixels, in every grid. A row's
height is not rounded, so a horizontal gridline is exact only where the row height times the ratio is
whole. That stays owed.

**Ctrl+Shift+= and Ctrl+- (case 12): ADR-0050, item 14.** ExSheet declares them. Whole rows insert or
delete rows, whole columns insert or delete columns, and any other Selection changes nothing and says
why. They are the browser's zoom keys, and inside a Sheet they are ExSheet's.

**`####` fills the cell (case 3c): [ADR-0016](./0016-column-width-and-overflow.md)'s note of this
date.** Six `#` were not the cell full: `#` was charged the wide class's width, 14.04px, which is a
bound for whether a number fits and not the width of `#` in the face the cell is painted in. The browser
now cuts the run at the last whole `#` that fits, so the cell is full of `#` in its own face, as Excel's
is. Whether a number fits is decided as before.

**The Selection on the Paper (case 11).** On a Sheet the Selection looks like Excel's. ExGrid keeps its
own. Under the built-in Chrome:
- **The outline is 2 Device Pixels of `#217346`, on the gridline and one Device Pixel outside it,
  with a white line of one Device Pixel inside it.** Today it is 2 CSS px in the Ink. The single range's
  outline and the Focus's outline are drawn alike.
- **The shade is `#C7C7C7`, and the lines inside it stay as drawn.** Excel's black lines stay
  `#000000` over its shade.
  - The shade multiplies with what is beneath it, rather than lying over it. On white Paper it is
    `#C7C7C7`. Over a Fill it darkens the Fill. A black line, and the darkest pixels of any line, stay
    black. That is what case 11 read of Excel, and it is the one way an overlay can leave lines
    unchanged: the Selection is painted as overlay rectangles, never as a class on the selected cells
    ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)), so it cannot pass between a cell's Fill
    and its lines.
  - So **DC-59 changes for the Paper only**: a line lies above the Selection's shade, and below its
    outline and the Focus's. A coloured line is darkened by the shade as a Fill is. Excel was not read
    over a coloured line, so that is a reading.
  - **Not in a Pinned Column.** The pinned part of the Selection stands in a layer of its own, above the
    pinned cells (ADR-0004, ADR-0008), and a layer can only multiply with what is inside it. So over a
    Pinned Column the shade still lies over the lines, as before. That stays owed.
- **The Focus cell stays unshaded**, as today.
- **The fill handle is the outline's colour**, as Excel's is: green under the built-in Chrome, the
  primary under `ExSheet.MudBlazor`.
- **The shade's default is black at 22%.** Multiplied over white Paper it is `#C7C7C7`. Over a Pinned
  Column, where it cannot multiply, it stays translucent, so the value still reads through it.
- **While an edit is open, the white line inside the outline is not drawn**, as Excel drops it (the
  fourteenth run, case 19).

Under `ExSheet.MudBlazor`, the outline is the palette's primary, and the shade is the primary mixed
into white, so the two read as one colour, as Google Sheets' do. The white line inside the outline
stays. Each is a Visual Token on the Paper (`--ex-selection-outline`, `--ex-focus-outline`,
`--ex-selection-fill`, and `--ex-sheet-selection-ring` for the white line), so a Wrapper or a Consumer
sets them as any other. This is the exception that ADR-0027's note of 2026-09-30 already makes for the
Focus under a Wrapper: the Paper and the Ink are not the Wrapper's, but what the grid draws over them
is.

**Format Cells' *Normal font* box (case 24).** The Font tab gains it. Checking it sets every part of the
Font to its default: the colour Automatic, and bold, italic, underline and strikethrough off. OK
records those parts on each selected cell, as any part touched is recorded, so a row's or a column's
Font no longer shows through there. Excel writes the Normal style's font on the cell, which comes to
the same. It shows checked while every part the draft holds is the default, and unchecked once one is
changed. ExSheet has no Normal style, and the box does not give it one: a typeface and a size stay out
(above).

**Seen beside the readings.**
- **The Row Headings' edge is `#ABABAB`**, Excel's, on a Sheet: the heading rule's grey at the alpha
  that gives `#ABABAB` over white, so it still follows the page's scheme.
- **`ExSheet.MudBlazor`'s Format Cells shows all five tabs whichever it opens on.** It no longer opens
  on Border with Font and Fill scrolled out of the tab strip.
- **`#DIV/0!`'s error triangle and the size of a Sheet's cells** each need a decision of their own,
  and are not decided here.
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.

## Consequences

- **Notes on other ADRs**, each saying what changed:
  - ADR-0046: the first version now holds fonts, fills and borders.
  - ADR-0047: `[Red]` is painted.
  - ADR-0050: items 14 to 16.
  - ADR-0010 and ADR-0040: a seam whose frame is the Chrome's.
  - ADR-0019: `ExSheet.MudBlazor`.
  - ADR-0048: the Sheet Document records the new parts.
  - ADR-0027 and ADR-0030: the Paper and Ink tokens are not the Wrapper's to darken, and bold
    widths join the metrics a Wrapper supplies.
- **The Sheet Document gains Font, Fill and Border, at a new version.** Version 7 is already taken
  on `claude/exsheet-pointing-scope`, so the number is fixed when the branches merge. *(2026-10-01:
  it is version 8. Pointing Scope reached `claude/exsheet-start-8cx3v1` first, with version 7 for a
  Linked Table's key (ADR-0049).)*
- **The code's names follow the glossary.** `AxisStyle` becomes `AxisFormat`, `SetStyle` becomes
  `SetCellFormat`, and `Sheet.SetFormat` becomes `SetNumberFormat`. `ExSheet.Engine` is not
  published (ADR-0046), so the renaming breaks nobody.
- **New criteria**: SH-38 to SH-47 and DC-57 to DC-60 in the Definition of Done; SH-53 to SH-55 after Part C (numbered SH-48 to SH-50 until the merge with ADR-0100's, which took those first), and DC-59 and FN-12e amended.
- **Tickets** 44 to 58 and 81 to 103, in `docs/specs/exsheet/issues/`:
  - ticket 55 was added by the eleventh run;
  - 56 by ticket 51's Server fix;
  - 57 and 58 by the twelfth run;
  - 81 and 82 by ticket 47, 83 by ticket 82, 84 and 85 by ticket 81, 86 to 90 by ticket 48, 91 by ticket 83, 92 by ticket 88, 93 by PR #42's CI, 94 by the user (ADR-0006's note of 2026-10-01), 95 and 96 by ticket 94, 97 by ticket 96, and 98 by ticket 97, for the PR after #42;
  - 99 to 103 by the fourteenth Windows run;
  - 140 to 147 by what was decided after Part C.

  59 to 75 are `claude/exsheet-start-8cx3v1`'s, and 76 to 80 are Pointing Scope's line
  (`docs/agents/numbering.md`).
