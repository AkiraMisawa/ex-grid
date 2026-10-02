# The grid is told its Device Pixel, and puts column edges on it

*(Decided with the user on 2026-10-02, after Part C of the eleventh Windows run
(`verification/2026-10-02-windows-excel-11c/beside-excel.md`). ADR-0071's "What Part C of the
eleventh Windows run found" left this to the next PR, because it needs two things that ADR did not
decide.)*

## What Part C found

At a display scale of 150% and a browser zoom of 100% (`devicePixelRatio` 1.5), a Sheet's lines read
as Excel's, with one exception: **a vertical gridline**. A column is 99 CSS px, which is 148.5 Device
Pixels, so every second column edge lies on half a Device Pixel. A one-CSS-pixel rule there is drawn
over two Device Pixels, `#F0F0F0` and `#E0E0E0`, where Excel draws one of `#E0E0E0`. A horizontal
gridline is one Device Pixel, because a row of 28 CSS px is 42 Device Pixels.

At a browser zoom of 150% on that display (`devicePixelRatio` 2.25), the stylesheet takes the Device
Pixel from the nearest `resolution` step below, 2. Every line that is drawn in Device Pixels (ADR-0071's
thin, medium, thick, double and dashed lines, and the row's rule, `--ex-rule-dp`) is then drawn at the
wrong size: a gridline is two Device Pixels, thin covers one of them, thick is not centred on the
gridline, and the long dash is 10 or 11. The same holds at any resolution between two steps.

So exactness at every scale needs two things:

1. **The grid has to know the Device Pixel at every resolution**, not only at the steps the stylesheet
   lists.
2. **Column edges have to lie on Device Pixels.** No width given to a line makes a line exact on an
   edge that lies between two of them.

## The decision

### 1. The browser tells the grid its Device Pixel

**ADR-0021 gains an entry: a `matchMedia` listener that reports `devicePixelRatio` at attach, and again
whenever the resolution changes.** The listener watches `(resolution: <current>dppx)`. When that query
stops matching, the display scale or the page zoom has moved, and the listener reports the new
`devicePixelRatio` and arms itself again on the new value.

- **It is told, not measured** (the distinction ADR-0021 draws for the fourth and fifth entries). The
  grid reads nothing on the path to a paint. The browser already knows the resolution, and the
  listener fires only when it changes: at attach, on a zoom, and when a window moves to a display of
  another scale. Never per render.
- **No Blazor API carries it.** Blazor has no media query listener and no `devicePixelRatio`.
- **Before the first report**, the stylesheet's `resolution` steps stand, as they do today. They are
  the fallback, not the answer.
- **Emulation.** ADR-0053 found that `devicePixelRatio` does not say which Layout Ceiling applies,
  because emulation moves the scale and the zoom apart. It does say how many Device Pixels a CSS pixel
  is painted on, which is the only thing this entry is used for. The Layout Ceiling keeps its own
  observer.

### 2. The Device Pixel is written inline on the instance root

C# writes `--ex-dp`, the size of one Device Pixel in CSS pixels, inline on the instance root from the
reported ratio. It is geometry: the grid computes with it (below), so it is not a stylesheet value
alone (ADR-0027). The stylesheet's lines already read `--ex-dp`, so every line drawn in Device Pixels
is exact at any resolution once it is told.

### 3. Column edges are put on Device Pixels

**Each column's left edge is its position in the content, rounded to the nearest Device Pixel; its
painted width is the distance to the next edge.** The positions are summed first and rounded after, so
the total width is the declared total rounded once, and the rounding never accumulates.

- **One place does it**: where the grid builds its column geometry from the resolved widths. Every
  reader of that geometry — the cells, the header, the Selection and the other overlays, the Cell
  Editor and the Keyboard Field, the hit test and the `####` decision — reads the same edges, so none
  can drift from another (ADR-0013).
- **The Row Headings' width is rounded the same way**, so column 0's edge lies on a Device Pixel too.
- **A column's rule is drawn in whole Device Pixels**, as a row's already is (`--ex-rule-dp`): the
  rule's width rounded down to the Device Pixel, and never thinner than the token where the token is
  under one. Until now it kept the CSS width, because no width made a line exact on an edge between two
  Device Pixels. On an edge that lies on one, 1px is one Device Pixel at 150%, as Excel's gridline is.
- **Every grid, not only a Sheet.** A one-pixel column rule on half a Device Pixel is blurred under
  ExGrid as under ExSheet, and the row's rule (`--ex-rule-dp`) is already every grid's.
- **What a column is declared to be does not change.** The View State, a resize's report and Auto
  width keep the widths as declared. Only the painted geometry is rounded. A painted width differs from
  the declared one by under one Device Pixel, and two columns of one declared width may be painted one
  Device Pixel apart.
- **The `####` decision reads the painted width**, because that is the space the text has. A value that
  fits its declared width with less than a Device Pixel to spare may be `####` at one scale and shown
  at another. That is the direction ADR-0016's rule allows: an early `####` costs a hover, a cut number
  a misread.
- **A change of resolution rebuilds the geometry** as a change of width does. The rows repaint once,
  at the zoom, and skip as before afterwards (ADR-0003).

## What stays out

- **Rows.** A row's height is not rounded. Rows lie on Device Pixels wherever the row height times the
  ratio is whole: a 28 CSS px row at 100%, 125%, 150%, 175%, 200%, 225% and 250%. At 110%, 90%, 80% and
  67% it is not, and a horizontal gridline lies between two Device Pixels. Rounding the row height
  changes the virtualisation arithmetic every reader shares (ADR-0013, ADR-0053), so it is a decision
  of its own, owed.
- **Where the page puts the grid.** If the page lays the grid's box at a fraction of a Device Pixel,
  every edge inside it is offset by that fraction. ADR-0053's slice is rounded inside the grid; the
  grid's own position is the page's.
- **The fallback steps.** Until the first report, a resolution between two steps still draws a little
  under a Device Pixel, as before.

## Considered options

- **More `resolution` steps in the stylesheet.** It would need a step for every zoom on every display
  scale, and a ratio between two steps would still be wrong. It also gives C# nothing to round the
  column edges with.
- **Reading `devicePixelRatio` when a render needs it.** That is a synchronous read on the path to a
  paint, which ADR-0021 refuses, and it would not say when the ratio changed.
- **Rounding each column's width instead of its edge.** Every column would be exact, but the total
  would drift by up to half a Device Pixel per column, so a wide grid's last edge, and a scroll
  position restored from the View State, would land somewhere else at each scale.

## Consequences

- ADR-0021 lists the entry, and `AGENTS.md` counts it.
- `CONTEXT.md` defines **Device Pixel**.
- ADR-0071's Part C item is answered by this ADR, but for rows, above.
- New criteria in the Definition of Done: VZ-16 and VZ-17.
- Tickets 140 and 141 build it.
