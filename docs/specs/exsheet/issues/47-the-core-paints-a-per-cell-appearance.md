# 47: The core paints a per-cell appearance, and judges bold by bold widths

Status: ready-for-agent

**What to build:** [ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md), item 15, from
[ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md). A Consumer can supply a cell's Font, Fill and four Border sides. A bold cell is judged by bold
widths.

**Blocked by:** None. Ticket 44's measurement decided the painting (ADR-0063, "What the measurement
chose"): interned classes for Font and Fill, and Borders drawn inside each cell, each painting its
share of Excel's centred line from edges resolved once per row outside the render. The spike is
`spikes/render-bench/Bench.Client/` (`CellFormatModel.cs`, `Components/FormatRow.razor`, mode
`BorderInCellExcel`).

- [ ] **The declaration**: a per-cell appearance (Font colour, bold, italic, underline,
      strikethrough; Fill; four Border sides), off by default (DC-1).
  - It says how a change reaches the rows, in the shape ticket 44's decision fits: ADR-0006's lookup
    identity, or per row.
- [ ] **Painting**: Font and Fill are painted by the mechanism ticket 44's decision chose (DC-58).
  - P1–P9 hold.
  - A row repaints only when its Values or its appearance changed.
  - Nothing per cell reaches JavaScript.
- [ ] **Borders are drawn as DC-59 says**: centred on the gridline, a thick line into both cells,
      above Fills and below the Focus, the Selection and the Reference Outlines. Only painted rows
      are drawn. The line for an edge recorded on both sides is the Consumer's answer.
- [ ] **Bold widths** (DC-58).
  - `CellTextMetrics` gains them: each character class at the bold weight. The core's defaults for
    its own font are measured as §21.7a measured the regular ones.
  - A bold cell's `####` decision, and the width handed to painted text, use them.
- [ ] `ExGrid.MudBlazor` supplies bold widths for its font (ADR-0030).
- [ ] **Measure two variants in `spikes/render-bench`** (ADR-0063, "Still owed").
  - The hybrid: solid lines as the cell's own `border`, with background layers only for dashes,
    double and the pixel past the gridline. Interning stays per side, style and colour, never per
    combination of four sides. Take it if it draws the same pixels and costs less.
  - A Fill and a border on the same cell.
- [ ] **Tests.**
  - Layer 2: render counts and markup.
  - Layer 3 pixels:
    - a bold number at the width where regular fits and bold does not;
    - italic not cut at either edge;
    - borders at 100% and 150%.

## Comments

2026-10-01, agent cf-47.

### What was built

- **The declaration** (`src/ExGrid/Cells/CellAppearance.cs`; parameters on `ExGrid<TRow>`):
  - `CellAppearanceOf<TRow>`, `CellAppearance (TRow row, GridColumn<TRow> column)`, as the
    `CellAppearance` parameter. Null by default, and then nothing is painted for it (DC-1: no
    `<style>`, no class, every row's `Appearance` null).
  - `CellAppearance`, a record struct with init properties: `RgbColour? FontColour`, `bool Bold`,
    `Italic`, `Underline`, `Strikethrough`, `RgbColour? Fill`, and `Border Top`, `Right`, `Bottom`,
    `Left`. `CellAppearance.None` is the default.
  - `Border(BorderStyle style, RgbColour colour = default)`: one of Excel's thirteen
    `BorderStyle`s, or `None`. `RgbColour.FromRgb(int)` / `FromRgb(r, g, b)`, `Black` the default.
  - `EdgeBorderOf`, `Border (Border upperOrLeft, Border lowerOrRight)`, as the `EdgeBorder`
    parameter: which line an edge draws when both cells record different ones. Asked only then;
    without it the upper or left cell's line is drawn (`CONTEXT.md`, Border).
  - **How a change reaches the rows**: ADR-0006's identities. The row instance and the lookup are the
    change signal, as for `CellState`. The grid resolves each painted row's appearance once, outside
    the row's render, from the row and the rows either side of it in the Window
    (`Components/CellAppearances.cs`), into an immutable `RowAppearance` that `ExGridRow` compares by
    reference. A row keeps its instance while it would paint the same, so a row repaints only when
    what it paints changed: a new instance repaints its own row, and a neighbour only where a line
    they share moved; a new lookup asks every painted row again and repaints only those it changed.
- **Font and Fill**: interned classes in a stylesheet the grid generates at the end of its root
  (`Components/AppearanceStyles.cs`), one rule per distinct Font and per Fill colour, named by what
  they paint (`ex-font-ff0000bi`, `ex-fill-ffff00`), so two grids on a page never disagree about a
  class. A Stale or Error Cell State keeps its look over a Font (ADR-0006); a Font outranks a
  column's tone. Bold is weight 700.
- **Borders** (DC-59): each cell draws its own share of Excel's centred line. A solid line on the
  gridline (thin, medium, thick's upper two pixels) is the cell's own bottom or right border, a right
  one giving its width back out of the padding so no text moves; dashes, double, the pixel a thick or
  double line reaches past the gridline, and a neighbour's Fill over the gridline a cell holds are
  background layers, read by one static rule (`.ex-lined` in `ex-grid.css`) from custom properties.
  Rules are interned per side, style and colour, never per combination. Lengths are device pixels
  (`--ex-dp`, by resolution), and the long dash is 8 at 100% and 9 at 1.5dppx (`--ex-dash`), as case
  9 recorded. Lines lie above Fills (background images over the background colour) and below the
  Focus, the Selection and the Reference Outlines (the overlay layers above the rows). A Fill covers
  the gridlines at all four of its edges: its own colour covers the row's rule and drops its column
  rule, and the neighbours that hold its top and left gridlines paint them in its colour, beneath any
  line. Only painted rows draw anything, and nothing is painted outside a row.
- **Bold widths** (DC-58): `CellTextMetrics` gains `BoldWideWidthPx`, `BoldDigitWidthPx`,
  `BoldNarrowWidthPx`, an eight-argument constructor, `WithBoldWidths(...)`, and `Bold`, the metrics
  a bold cell is judged by. A bold cell's `####` decision, the width and metrics its painted text is
  fitted with, and an Auto width or a fit over it use them. Measured as §21.7a measured the regular
  ones (Chrome, 14px, tabular digits, each class's glyphs): at weight 700, system-ui on macOS
  14.35 / 9.25 / 5.852 (wide / digit / narrow) and DejaVu Sans Bold, which Linux paints for 600 and
  700 alike, 14.028 / 9.742 / 6.398; the defaults are the widest, 14.35 / 9.742 / 6.398 (Excel's
  12px: 12.3 / 8.351 / 5.484). Metrics built without bold widths charge 4% over the regular ones
  (`BoldWidthAllowance`), the widest growth measured from weight 600 to bold (`(` in system-ui on
  macOS). `GridPresentationDefaults` takes bold widths too.
- **`ExGrid.MudBlazor`** supplies Roboto bold: `%` 10.352, `£` 8.323 (the digit 8.036), `)` 4.917,
  declared 10.4 / 8.33 / 4.95. `MudExGridFont` takes optional bold widths.
- **The `/appearance` demo page** and `appearance.spec.mjs`.

### The measurement (`spikes/render-bench`, "Measure the hybrid and a Fill with borders")

Three sweeps of the same configurations, each mode beside a `RowComponent` baseline of its own,
interleaved: `BorderInCellExcel` (ticket 44's), `BorderInCellExcelFull` (every solid share a
gradient over the whole cell, what this ticket first built), `BorderInCellHybrid`, and
`BorderInCellExcelFill` / `BorderInCellHybridFill` (a Fill of one of N colours on every bordered
cell). Headless Chrome 154 on the Apple M4 Pro, fling frames of 40 × 20 cells.

| File | Load (1-min) | Counts? |
|---|---|---|
| `results/20261001-183929-354.json` | 3.9 to 25.5 | no: the deltas swing by up to 20 ms with the load |
| `results/20261001-190312-851.json` | 3.6 to 11.7 | yes |
| `results/20261001-192214-388.json` | 3.5 to 14.1 | yes |

Fling frame, Δ p50 against the baseline beside it (second sweep · third), ms:

| Share | N | Base p50 | Excel | ExcelFull | Hybrid | ExcelFill | HybridFill |
|---|---|---|---|---|---|---|---|
| 10% | 1 | 13.5 · 13.4 | +0.5 · +0.5 | +0.6 · +0.7 | +0.3 · +0.5 | +0.6 · +0.7 | +0.4 · +0.5 |
| 50% | 16 | 13.7 · 13.8 | +2.0 · +2.2 | +2.2 · +2.3 | +2.4 · +2.5 | +3.7 · +3.8 | +3.0 · +3.1 |
| 50% | 256 | 13.7 · 13.8 | +6.0 · +5.1 | +5.2 · +5.4 | +4.9 · +4.8 | +7.7 · +8.4 | +7.5 · +7.6 |
| 100% | 1 | 13.6 · 13.9 | +0.9 · +0.9 | +0.9 · +0.9 | +0.2 · +0.4 | +1.1 · +1.0 | +0.4 · +0.3 |
| 100% | 16 | 13.6 · 13.9 | +1.7 · +1.9 | +1.8 · +1.9 | +1.5 · +1.4 | +4.2 · +4.5 | +3.9 · +3.9 |
| 100% | 256 | 13.7 · 13.9 | +5.3 · +5.5 | +5.5 · +6.1 | +4.7 · +5.6 | +10.4 · +11.1 | +10.2 · +11.1 |

Δ p95, same order: 10%/1 +0.4 to +0.7 everywhere; 50%/16 Excel +3.3, Full +3.5/+3.9, Hybrid
+4.7/+4.1, fills +5.0 to +5.3; 50%/256 Excel +8.4/+7.6, Hybrid +7.1/+7.5, fills +11.8 to +12.3;
100%/1 Excel +1.0/+1.9, Hybrid +0.1/+0.4; 100%/16 Excel +2.6/+3.3, Hybrid +2.1/+2.6, fills +5.9 to
+6.5; 100%/256 Excel +8.2/+8.3, Hybrid +7.1/+7.9, fills +14.5 to +15.6.

- **Rows skip in every mode and configuration** (P2/P3 as the bench checks them): a slow step mounts
  one row, nothing renders with nothing changed, one cell's border repaints at most 2 rows.
- **The hybrid costs less** than the layers in five of six configurations in both clean sweeps, by
  0.2 to 0.8 ms at the median and up to 1.5 at p95, most at 100% N 1 ("All Borders"); at 50% N 16 it
  costs 0.2 to 0.4 more. A column change, which relays out 40 rows' borders, costs it about 0.5 ms
  more (10.7 against 10.2).
- **The hybrid draws the same pixels.** At device scale 1 and at 1.5 given on Chrome's command line
  (`--force-device-scale-factor`, the way an OS display scale and browser zoom lay out: in device
  pixels), it and the layers both draw case 9's table for all thirteen styles. Under CDP's
  device-scale emulation (what Playwright's `deviceScaleFactor` does), which lays out in CSS pixels
  and scales, a border is drawn over 1.5 device pixels and a whole-cell gradient is not; there the
  first build's layers were exact for the solid styles and the hybrid is not. That is not a display
  a user has, so the product takes the hybrid, as the ticket's rule says.
- **A Fill and a border on the same cell** add +0.1 to +0.2 ms with one Fill colour, +1.5 to +2.6
  with 16, and +2.6 to +5.6 with 256 distinct Fills over 256 distinct lines, over the border alone.
  The Fill variants intern one class per colour; their extra cost is mostly style recalculation
  (the layout column), as ticket 44 found for many distinct lines.

### The pixels (`/appearance`, before layer 3)

Checked on a static copy of the grid's own markup and stylesheet in headless Chrome 154: at 1 and at
1.5 (`--force-device-scale-factor`), all thirteen styles match case 9's table — thin on the gridline,
medium on it and the pixel above, thick a pixel either side, double either side of a white gridline,
the dash patterns 1/1, 2/2, 3/1, 8 (9 at 150%)-3-3-3 and so on, slanted dash-dot as Excel's two rows
(`###########.#####.` over `#########..####..`); a red thick line over a yellow Fill keeps its
third pixel above the Fill; a right border leaves the number 4px from the cell's edge, as without it.

### Tests

- Layer 1: `BoldWidthTests` (11 tests, 14 cases). Layer 2: `CellAppearanceTests` (25): markup, the generated rules,
  render counts (a held lookup renders nothing again; a new instance with the same lines repaints no
  neighbour; a moved line repaints the rows either side of its edge and no other; a new lookup
  answering the same repaints nothing), the bold `####`, the bold metrics handed to painted text and
  to a fit, Pinned Columns, a sideways pan, and no new JavaScript. All of layers 1 and 2 pass:
  ExGrid.Tests 871, ExGrid.Components 1119 (one skipped), ExGrid.MudBlazor.Tests 88,
  ExSheet.Engine.Tests 2107, ExSheet.Components.Tests 414.
- Layer 3 (`appearance.spec.mjs`; DC-59 also in `chrome-150`): not yet run, waiting for the slot.
