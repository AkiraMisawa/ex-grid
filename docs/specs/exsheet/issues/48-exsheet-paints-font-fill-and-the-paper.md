# 48: ExSheet paints Font and Fill on white Paper

Status: needs-info

**What to build:** the component's half of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "What a Cell Format holds" and "Paper and Ink".

**Blocked by:** None (47 is in)

- [x] **ExSheet declares ADR-0050 item 15** from the engine's Cell Format. A Number Format's colour
      (ticket 46) takes the Font colour's place.
- [x] **The Paper and Ink tokens** are `--ex-sheet-paper` and `--ex-sheet-ink`, with Excel's white
      and black as defaults in every scheme (SH-39).
  - On the Paper, the Focus, the Selection, Reference Outlines (with the pointed shade) and the Cell
    Editor keep their light-scheme appearance.
  - The Headings, the Name Box, the Formula Bar and popovers follow the scheme.
  - ADR-0027's note of 2026-09-30 permits this exception. Say in a comment how it is done.
- [x] Neither `ExGrid.MudBlazor`'s stylesheet nor `ExSheet.MudBlazor`'s `mud-ex-sheet.css` sets the
      Paper tokens (inspect, SH-39). Layer 3 "under both Chromes" runs `/sheet?chrome=mud`, which is
      `MudSheetChrome` since ticket 53.
- [x] A Fill or Font on a whole row or column paints cells that hold nothing, so a change to a
      level repaints every painted row it covers. `SheetChange.Rows` lists only rows that hold a
      cell, so it does not cover them (found by ticket 45).
- [x] A row repaints only when its Values or its Cell Format changed (SH-4, DC-58).
- [x] **Layer 3** under the light and the dark scheme, under both Chromes, on both hosts: recorded
      colours read as recorded, and a clean console.

## Comments

2026-10-01, agent cf-48.

Every box is built and tested. The status is `needs-info` for one finding: under
`ExSheet.MudBlazor`'s Chrome, the Ink makes an existing DC-48 drift fail its threshold (the last
section). It needs a decision.

### What was built

- **The declaration** (`src/ExSheet/SheetAppearance.cs`, `SheetRow.AppearanceAt`). ExSheet hands
  its grid ADR-0050 item 15's `CellAppearance`. Each cell's Font (colour, bold, italic, underline,
  strikethrough) and Fill come from the engine, cell over row over column (`Sheet.GetFont`,
  `Sheet.GetFill`).
  - The colour of the Number Format section that shows the Value replaces the Font colour
    (`CellDisplay.Colour`, now carried on `SheetCellText`), and a `####` keeps it.
  - An Automatic Font colour declares no colour, so the text is the Ink.
  - Borders are not declared yet. They are ticket 49's.
- **Repainting.** A row keeps its answers for one *reading* of the Sheet's formatting. A change the
  engine names retires the row, as before.
  - A change that may reformat rows the engine does not name starts a new reading. ExSheet hands
    the grid a new lookup, and the grid asks every painted row again and repaints only the rows
    whose look changed (ADR-0050 item 15's "a new lookup").
  - To say when, the engine's `SheetChange` gains **`ReformatsUnnamedRows`**. It is set when a
    whole row's or column's Cell Format is set, cleared or moved, including by an undo or a redo.
    It is also set when rows or columns are inserted or deleted, which moves the levels and brings
    two cells' sides together on a new edge. That edge is ticket 49's case: an empty row's bottom
    line changes when the row under it is deleted, and `Rows` does not name it (pinned in
    `UnnamedRowsTests`).
  - So a Fill on a whole row repaints that row, and the row above, whose gridline it covers, and
    nothing else. A whole column repaints every painted row. An entry repaints its row alone, as
    SH-4 says.
- **The Paper and the Ink** (`ex-sheet.css`, with a comment saying how the exception to ADR-0027
  is done). ExSheet divides its grid by element.
  - What lies on the Paper is painted from `--ex-sheet-paper` / `--ex-sheet-ink` (`#fff` / `#000`,
    in every scheme): the rows' ground, the cells' text, a Pinned Column's ground, a double line's
    middle pixel (`--ex-background` on the cells), and the Cell Editor in its cell.
  - Everything the grid draws over the rows stands in a light island (`color-scheme: light` on the
    viewport's children other than rows). That is the Focus, the Selection, Reference Outlines with
    the pointed shade, the fill handle, and the Cell Editor with its coloured text. Whatever they
    take from a system colour or from `light-dark()` resolves as in the light scheme.
  - What frames the Paper is left alone and follows the scheme: the column Headings, the Row
    Headings (which stand inside the rows, so the row's colour is not set), the Name Box, the
    Formula Bar, popovers, the Size Tip and the scrollbar.
  - The rules have one class's specificity (`:where(.ex-sheet)`), so the core's Fill and line
    rules win in any order. They reach the grid's internal elements (ADR-0029), as `ex-sheet.css`
    already did for the popover.
- **Gridlines** (agreed with the orchestrator, 2026-10-01).
  - ExSheet had none under the built-in Chrome, because the core's rule tokens default to
    transparent. Under ExGrid.MudBlazor it had only the Wrapper's row rules, which vanish on white
    Paper in MudBlazor's dark theme.
  - The Paper now carries Excel's gridlines, `#e0e0e0` as the eleventh run sampled them, on rows and
    columns under both Chromes, through the core's own `--ex-row-rule-color` and
    `--ex-column-rule-color`. They are the Ink mixed 12% into the Paper, which is exactly `#e0e0e0`
    at the defaults. No token was added.
  - **On the Sheet's Paper this overrides the Wrapper's row rules** (MudBlazor's table lines). Every
    other ExGrid on a MudBlazor page keeps them.
  - A Pinned Column's cell has an opaque ground that hid the row's gridline, so it paints the
    gridline itself unless a Fill covers it.
- **Neither MudBlazor stylesheet sets or reads the Paper tokens.** `PaperStylesheetTests` in
  ExSheet.MudBlazor.Tests inspects both, and checks that `ex-sheet.css` defaults them to `#fff` and
  `#000` with nothing scheme-dependent.
- **DemoHost.**
  - `/sheet?scheme=dark` declares `color-scheme: dark` for the document, and puts MudBlazor in dark
    mode under `?chrome=mud` (`DemoChrome.IsDark`). Any other value is refused by name.
  - `/sheet?case=N` opens one of the eleventh run's Part A set-ups instead of the page's own Sheet
    (`SheetCases`): 1, 2, 3b, 3c, 4, 5, 6, 16, 17 and 18, for Part C. `paper` gathers recorded
    colours, emphases, and a row's and a column's Fill.
- `src/ExSheet/README.md` has a section on the Paper and the Ink.

### Tests

- **Layer 1**: `UnnamedRowsTests`, 12 tests.
- **Layer 2**:
  - `SheetAppearanceTests`, 21 tests: the classes declared; SH-40's eight colours and cases 2, 3b
    and 3c; Fill and Font on empty cells at row and column level; render counts for a row, a column,
    a cell and an entry; an inserted row's Fill; SH-41's `####` and General at the bold widths.
  - `PaperStylesheetTests`, 3 tests.
- **Layers 1 and 2 in full**: ExGrid.Tests 871, ExSheet.Engine.Tests 2316, ExGrid.MudBlazor.Tests
  88, ExGrid.Components 1248 (one skipped), ExSheet.MudBlazor.Tests 37, ExSheet.Components.Tests
  554.
- **Layer 3**, headless on this Mac, `--project=chrome`:
  - `sheet-paper.spec.mjs` passed 9 of 9 on WebAssembly and 9 of 9 on Server. It covers both
    Chromes in both schemes: the Paper white and the Ink black; a Font colour, `[Red]` over a blue
    Font, and a cell's, a row's and a column's Fill, all as recorded; the emphases; the Headings and
    the Formula Bar following the scheme; the Selection, the Cell Editor, a Reference Outline and
    the pointed shade keeping the light look; the Formula Bar's References taking the dark shade;
    and the tokens set by a Consumer.
  - The `/sheet` specs that read colours or pixels, on WebAssembly (selection-look, reference-text,
    headings, sheet-vs-excel, declarations, pointing-scope, format-keys): 196 passed, 13 skipped,
    5 failed. Each failure was rerun alone, on this tree and on 2ff6dfd.
    - Four also fail on 2ff6dfd, so ticket 48 did not cause them:
      - DC-13, the edge auto-scroll carrying a fill.
      - The two clipboard cases of sheet-vs-excel, headless on macOS.
      - sheet-vs-excel item 26: since ticket 58, "Format selection as #,##0.00" widens the column,
        so the number no longer shows `####`. The test predates that.
    - One is ticket 48's: below.

### DC-48 under ExSheet.MudBlazor, at End: needs a decision

`reference-text.spec.mjs` › "DC-48: a Formula longer than the Cell Editor keeps its colours over the
right characters at either end (mud Chrome)" fails at End: 2 pixels are "apart" where it allows 0. It
passes on 2ff6dfd, and the other three DC-48 tests pass here.

Measured with the same test on both trees:
- Under the Mud Chrome, the Cell Editor at End has 112 pixels that differ between the field's own
  text and the coloured layer's, on both trees. The drift is the same.
- On 2ff6dfd they were drawn in MudBlazor's text colour, `#424242`, and none differed by more than
  the test's 96 levels.
- Ticket 48 draws the Cell Editor in the Ink, black, as the cell is drawn. The same sub-pixel drift
  then exceeds 96 at 2 pixels.
- The built-in Chrome's editor was black before and after: 88 pixels differ there, none by more
  than 96.

The drift is ADR-0057's coloured layer against the field, with Roboto, at the far end of a long
Formula: the per-span snapping the test's comment describes. Ticket 48 did not change it; it made
the drift show at full contrast. Three ways out, each a decision:
- (a) fix the layer's drift under the Wrapper's font;
- (b) judge "apart" relative to the ink's contrast, which loosens the built-in Chrome's check too;
- (c) leave the Cell Editor's text in the Wrapper's colour under MudBlazor, which makes the text
  change colour when an edit opens.

I recommend (a), and I have not done any of them.

### Seen on the way, not built

- **The Cell Editor shows the Paper and the Ink, not the cell's Fill and Font.** In Excel a
  yellow, bold, red cell stays so while it is edited. The core's editor box takes no appearance.
- **The engine refuses `0;[Red]@`**, which Excel took in case 3b ("@ belongs in the fourth
  section"). Case 3b on the DemoHost leaves that cell out.
- **Under ExSheet.MudBlazor the Focus on the Paper is the Wrapper's** primary, raised in the dark
  scheme for a dark ground (ADR-0030). It reads against white, at 3:1 or more, but it is not the
  light palette's primary, which a stylesheet cannot reach in MudBlazor's dark mode.
- **A Pinned Column's cell that draws a line layer** (`.ex-lined`) loses its row gridline, because
  the core's line rule replaces the cell's background image. This is ticket 76's area (a pinned
  cell's tint under lines).
