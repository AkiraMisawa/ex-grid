# Apply list: the Heading Mark (claude/heading-mark)

### 1. docs/adr/0008-selection-is-painted-by-an-overlay.md

Old:

```
  - *Decided, not yet built* *(2026-10-13, decided with the user)*: Excel's Heading mark is added
    for every selection, not only several ranges — the selected rows' and columns' Headings marked,
    with a line of 2 Device Pixels on their edge toward the cells, as R4 pictured them. It is drawn
    as an overlay in the Heading bands, so no row learns the Selection; its classes, its tokens and
    the products that draw it are written here when it is built.
```

New:

```
  - *The Heading Mark* *(2026-10-13, decided with the user)*: Excel's Heading mark is added for
    every selection, not only several ranges — the selected rows' and columns' Headings marked, with a
    line of 2 Device Pixels on their edge toward the cells, as R4 pictured them. It is drawn as an
    overlay in the Heading bands, so no row learns the Selection. Built: the bullet that follows.
- **The Heading Mark** *(built 2026-10-13, as decided above; the products it reaches and its colour's
  default decided with the user the same day)*. For every Selection, each Heading whose column or row
  holds part of it carries a line of two Device Pixels on its edge toward the cells: along the bottom
  of a column's Heading, down the right edge of a row's. That is where the readings of 2026-10-12
  measured Excel's: two device pixels of `#217346` at Excel's 100% and 150% zoom on a 150% display, over
  the Heading's own `#ABABAB` edge and the device pixel beside it (R4), and the same over whole columns
  and whole rows (R5 (1) and (2)).
  - *Which Headings.* The union of the ranges' columns, and of their rows, as runs: neighbouring marked
    Headings carry one line, unbroken, as Excel's ran on under A and B for A1:B2, and a Heading between
    two ranges carries none, as C and row 3 carried none between A1:B2 and D4:E6 (R4 (2)). The Focus
    marks nothing of its own: it is always inside a range
    ([ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)), and Excel
    marked A1's Headings as it marked B2's (R4 (2)). One cell and one ordinary range are marked by
    generalisation: R4 read several areas, and R5 whole columns and rows.
  - *Where it is drawn.* Wherever a header or Row Headings stand, because the core draws it: a Sheet's
    Headings, a plain grid's header and its Row Headings, and a Pivot Report's header *(the products,
    2026-10-13, decided with the user)*. One box per run, the Headings' own, placed by the core inline
    ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)): over the header band's
    last tier, where the leaves stand beneath any Header Group
    ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)), and over the Row
    Headings in a sticky layer of the Viewport, cut to the painted rows as every rectangle here is
    ([ADR-0053](./0053-the-scroll-height-is-compressed-above-the-browsers-layout-ceiling.md)). The line is the box's border on its
    edge toward the cells, so it lies on Device Pixels wherever the band's edge does
    ([ADR-0090](./0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md)), and nothing in
    C# reads its width. A column's box passes beneath the pinned header cells and the Headings' corner
    as the column does; a Pinned Column's part rides a sticky layer of its own at the band's left edge,
    as a pinned Header Group does ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)). No row is
    told: rows still skip their render when the Selection moves
    ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)), and the cost is a box per run in each
    band, never one per Heading.
  - *Its classes and its token.* The internal classes `ex-heading-mark`, joined by
    `ex-heading-mark-column` or `ex-heading-mark-row` for the edge its line is on, and the layers
    `ex-heading-marks-pinned` and `ex-heading-marks-rows`
    ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)). Its colour
    is the Visual Token `--ex-heading-mark` *(2026-10-13, decided with the user: where nothing names it,
    it is the Focus outline's colour, `--ex-focus-outline`, so a Theme or a Wrapper that sets only the
    Focus outline gets both)*: `CanvasText` on a bare grid, `#217346` on a Sheet, and the palette's
    primary under every MudBlazor Wrapper, none of which maps it. Its width is two Device Pixels,
    `--ex-dp`, and is not a token.
  - *Under forced colors* the line is kept, a border, restated in `Highlight` as a range's outline is.
  - *Observed and not built*, raised with the user for a decision: R4 and R5 also read each marked
    Heading shaded, `#E1E1E1` where its column or row holds part of the Selection and `#D3F0E0` where the
    Selection holds the whole of it (from `#F0F0F0`), with its letters in `#217346` (from `#444444`), and
    the corner's triangle in `#217346` while every cell is selected (R5 (1)).
  - *Where it meets an outline*, raised with the user for a decision: an outline's side that stays
    inside its cell or range because the header or the Row Headings would cover its outer pixels
    (2026-10-01, above) lies beside the mark, so a Focus or a single range at the readable area's top
    row, or at column A beside the Row Headings, shows the outline's two Device Pixels and the mark's two
    together. R5 (2) pictured Excel's outline and its Heading's line as one line of two there. Built as
    both decisions read.
  - *Not observed:* one cell and one ordinary range; a display at 100%; where a row's line meets a
    column's under the corner (ExGrid's stop at the corner, which stands above them); frozen panes. On
    the eighteenth Windows run's list (`docs/specs/exsheet/run-18/heading-mark.md`).
```

### 2. docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md

Old:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-focus-among-ranges-outline` *(the thin border of the Focus among several ranges, added by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-10-12; (2026-10-12, decided with the user: it defaults to `--ex-focus-outline`))*, `--ex-grid-focus-outline` *(exists)* |
```

New:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-focus-among-ranges-outline` *(the thin border of the Focus among several ranges, added by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-10-12; (2026-10-12, decided with the user: it defaults to `--ex-focus-outline`))*, `--ex-heading-mark` *(the Heading Mark's line, added by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-10-13; (2026-10-13, decided with the user: it defaults to `--ex-focus-outline`))*, `--ex-grid-focus-outline` *(exists)* |
```

### 3. docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md

Old:

```
- **Forced colours** do not draw the border, and restate the Focus by its outline, as everywhere
  there.
```

New:

```
- **Forced colours** do not draw the border, and restate the Focus by its outline, as everywhere
  there.

## Added by ADR-0008's note of 2026-10-13

- **Internal classes**, which may change without notice: `ex-heading-mark`, one box of the Heading
  Mark over a run of marked Headings, joined by `ex-heading-mark-column` on a column's Headings, whose
  line is on its bottom edge, or `ex-heading-mark-row` on the Row Headings, whose line is on its right
  edge; and its layers, `ex-heading-marks-pinned` in the header band over the Pinned Columns' headers
  and `ex-heading-marks-rows` in the Viewport over the Row Headings. The core decides them from the
  Selection. UX-4's walk names them.
- **Visual Token**: `--ex-heading-mark`, the colour of the Heading Mark's line *(2026-10-13, decided
  with the user: where nothing names it, it is the Focus outline's colour, `--ex-focus-outline`, so a
  Theme or a Wrapper that sets only the Focus outline colours both, and no Wrapper maps it)*. On a
  Sheet that is `#217346`, the Focus outline's colour on the Paper, which the readings of 2026-10-12
  measured as Excel's line (R4). Its width is two Device Pixels, `--ex-dp`, and is not a token.
- **Forced colours** keep the line and restate it in `Highlight`, as a range's outline is.
```

### 4. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
    2026-10-12 observed there (R4; over a Fill, unobserved). Under `ExSheet.MudBlazor` it is the
    palette's primary, as the Focus outline is.
```

New:

```
    2026-10-12 observed there (R4; over a Fill, unobserved). Under `ExSheet.MudBlazor` it is the
    palette's primary, as the Focus outline is.
  - *The Headings' line.* The Heading Mark
    ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md), 2026-10-13) is the outline's colour on a
    Sheet, `#217346` under the built-in Chrome, as the readings of 2026-10-12 measured Excel's line (R4),
    and the palette's primary under `ExSheet.MudBlazor`. The Headings frame the Paper and follow the
    scheme; the line stands on them as drawn.
```

### 5. CONTEXT.md

Old:

```
_Avoid_: header (that is ExGrid's column header, whose click sorts), labels, row numbers
```

New:

```
_Avoid_: header (that is ExGrid's column header, whose click sorts), labels, row numbers

**Heading Mark**:
The line on a Heading's edge toward the cells that says its column or row holds part of the
**Selection** — along the bottom of a column's Heading, down the right edge of a row's — as Excel
marks its Headings. Every Selection has one, a single cell's included. ExGrid draws it wherever a
header or Row Headings stand, so a plain grid's column header and a Pivot Report's carry it as a
Sheet's Headings do ([ADR-0008](./docs/adr/0008-selection-is-painted-by-an-overlay.md)).
_Avoid_: header highlight, active heading, selected heading (the Selection is cells, never a Heading)
```

### 6. docs/definition-of-done.md

Old:

```
| **UX-20** | MUST | The Modified mark is a triangle in the cell's top right corner alone, painted in `--ex-state-modified-mark`, which is a colour — so a Wrapper that sets the token to a colour paints it — and a Pinned Column's Modified cell stays sticky (ADR-0029's fourth correction, 2026-10-07; ADR-0006) | Layer 3 on `/tones`: the corner's pixel against the Normal cell's, the top and right edges halfway and the pixels just outside the triangle against the same spots of the Normal cell, the token set to a colour above the grid, and the pinned cell's computed `position` | the corner differs; every other spot matches; the corner takes the token's colour; `sticky` |
```

New:

```
| **UX-20** | MUST | The Modified mark is a triangle in the cell's top right corner alone, painted in `--ex-state-modified-mark`, which is a colour — so a Wrapper that sets the token to a colour paints it — and a Pinned Column's Modified cell stays sticky (ADR-0029's fourth correction, 2026-10-07; ADR-0006) | Layer 3 on `/tones`: the corner's pixel against the Normal cell's, the top and right edges halfway and the pixels just outside the triangle against the same spots of the Normal cell, the token set to a colour above the grid, and the pinned cell's computed `position` | the corner differs; every other spot matches; the corner takes the token's colour; `sticky` |
| **UX-21** | MUST | The Heading Mark: for every Selection, each Heading whose column or row holds part of it — a column's header, a Row Heading — carries a line of two Device Pixels on its edge toward the cells, in `--ex-heading-mark`, which is the Focus outline's colour unless set (`#217346` on a Sheet, the primary under the MudBlazor Wrappers); neighbouring marked Headings carry one unbroken line and the Headings between two ranges none; it is an overlay, so no row renders for it; a Pinned Column's part stays over its header while a scrollable column's passes beneath the pinned headers; forced colors keep it, in `Highlight` (ADR-0008, 2026-10-13; ADR-0090) | Layer 1 for which Headings; Layer 2 for the boxes, their classes, the token and its default, and the rows' render counts as the Selection moves; Layer 3 by pixel on the Sheet under both Chromes, on `/wide` at 100% and at a real 150%, across the pinned boundary, under forced colors, and on ExPivot's report under both Chromes | as stated, in each case; no row's render count moves |
```

