# Selection is painted by an overlay, not by CSS classes on cells

The selection rectangle, the focus outline and the fill handle are painted as **absolutely
positioned overlay elements** layered over the rows. Putting a CSS class on selected cells is not
used.

## Measurements

A selection rectangle (10 rows × 8 columns) dragged down one row at a time. Scrolling is held
fixed, so the only thing changing is the selection. Rows are painted with the row component
settled in [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md). Milliseconds
per render.

**Real hardware** (Windows / Chrome 151 / 32 cores / .NET 10 Release, no AOT), 2000 cells (40×50):

| Approach | Median | p95 | **Max** |
|---|---|---|---|
| CSS class on cells | 2.80 | 3.40 | **19.70** ✗ over budget |
| **Overlay** | 1.20 | 1.40 | **2.10** |

The medians differ by 2.3×, but **the maxima differ by 9.4×** — and 19.70 ms is past the 16.6 ms
budget for a 60fps frame.

For reference (headless Chromium; software rendering, so the absolute values are not meaningful):

| Cells | Approach | Median | Max |
|---|---|---|---|
| 800 | CSS class on cells | 1.20 | 6.00 |
| 800 | Overlay | 0.80 | 1.00 |
| 2000 | CSS class on cells | 1.90 | 11.70 |
| 2000 | Overlay | 0.70 | 1.10 |

Real hardware has a **longer tail** than headless (max 11.70 → 19.70).

**The original prediction was wrong.** The prediction was "when the selection moves, every
selected row repaints, so it is heavy". In fact, moving the rectangle down one row **changes
membership for only the two rows at the edges**; memoisation keeps working for the rows in
between. The difference in medians is small.

**What separates the two approaches is not the size of the selection but how many rows change
membership at once** — which is what the maxima show.

| Operation | Rows changing membership | Class approach |
|---|---|---|
| Arrow key, one row | 2 rows | light |
| Shift+click to the edge of the screen | every visible row | full repaint |
| Ctrl+A | every visible row | full repaint |

Shift+click and Ctrl+A are among the most frequently used selection operations in Excel, and a UI
that repaints everything on exactly those is what "sluggish" means.

**The overlay's maximum does not grow with cell count** — one element is being moved, so it
depends on neither the number of cells nor the size of the selection. 2.10 ms maximum on real
hardware; 1.00 / 1.10 ms flat at 800 / 2000 cells headless.

## The non-performance reason

**Selection state never has to enter the row's parameters.** With the class approach, the
selection range must be passed into the row component in order to be visible, which mixes
something that is not data into the **Row Identity** of
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md). "The row changed" would
start to include "the selection changed".

With an overlay the row knows nothing about selection. **Row = data, overlay = selection**, and
the concerns stay apart.

## Consequences

- **Only geometric effects can be painted.** Rectangle fill, outline and fill handle: yes.
  "Invert the text colour of selected cells only": no.
- **The fill is translucent.** `rgba`, so the cell text shows through. This is also what Excel
  looks like.
- **Disjoint multi-range selection means one overlay per range.** Operations with many ranges are
  rare, so this does not become a problem.
- **The overlay lives in the same coordinate space as the rows.** Sharing the scroll translation
  with the rows means its position never has to be recomputed while scrolling.
- **It has to mesh with horizontal virtualisation.** Once columns are virtualised the x
  coordinate comes from cumulative column widths
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)). That arithmetic is needed for layout
  anyway, so it is not an added cost, but the implementations are coupled.
- **800 cells was not measured on real hardware.** Only 2000 cells (40×50) was confirmed there.
  With horizontal virtualisation
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)) the effective cell count drops to
  around 800, and the tail at that size has not been measured on real hardware. Since the overlay
  does not depend on cell count, the conclusion is not expected to change.
