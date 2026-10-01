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
