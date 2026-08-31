# Cap the number of cells touched per frame — horizontal virtualisation, and placeholders during fast scrolling

The grid **virtualises horizontally as well** (columns outside the viewport are not in the DOM).
In addition, **during fast scrolling it paints placeholder rows instead of real cells** and fills
them in once scrolling settles. Both serve the same end — capping the cells touched per frame —
and neither is sufficient alone.

## What the measurements showed

On real hardware (the table in
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)), **a fling over 2000
cells (40 rows × 50 columns) could not hold 60fps in any rendering mode**:

| Mode | 2000 cells, fling |
|---|---|
| Plain markup, no abstraction | 25.60 ms |
| Plain markup + accessor + metadata | 30.70 ms |
| Row as the boundary | 32.80 ms |
| Cell as the boundary | 92.00 ms |

By contrast, **at 800 cells (40×20) the plain-markup modes stay within 10.7–12.2 ms even during a
fling**. The boundary is therefore in the **cell count**, not in how they are painted.

And **row-level memoisation does not help during a fling**, because every row changes. The 10×
improvement it gives during slow scrolling does nothing for this worst case.

## Considered Options

- **Horizontal virtualisation alone** — insufficient. Narrowing 50 columns to 20 visible ones
  takes 2000 cells down to 800 and should land around 12 ms even during a fling, but that leaves
  only 4 ms of headroom. Adding rows, tightening the row height, or adding conditional formatting
  would cross it again easily.
- **Throttling during fast scrolling alone** — insufficient. On a wide screen, **the single frame
  after scrolling stops** still has to paint every cell for real, and that costs the full
  25–33 ms. The grid would stutter exactly once, at the moment it settles.
- **Switch to Canvas rendering** — rejected for now. The measurements found the ceiling of DOM
  rendering, but they equally found that DOM is sufficient at around 800 cells. Canvas means
  rebuilding text selection, accessibility, native scrolling behaviour and arbitrary cell content
  by hand, which gives up the foundation of Excel-like operability. Revisit **if the measures in
  this ADR stop being enough**.

## Consequences

- **Columns need a notion of "what is visible" too.** The same arithmetic as the vertical
  direction, applied horizontally. Pinned columns (a row-key column, for instance) are outside
  virtualisation and always painted, which makes this fiddlier than the vertical case.
  *(Refined while implementing — the shape it took, and the three places the two axes turned out
  not to be symmetric:*
  - *`ColumnGeometry` is the horizontal twin of `ViewportGeometry`, but the offsets are a
    **prefix sum** rather than one multiplication: there is no horizontal equivalent of the fixed
    row height ([ADR-0013](./0013-fixed-row-height.md)).*
  - ***Pinning is "the leading N columns"** (`PinnedColumnCount`), not a flag on the Column.
    Which columns are pinned is View State, which the Consumer owns and persists
    ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)) — combined with the
    column order it already owns, the leading N expresses it, and the grid holds no View State of
    its own. Excel's frozen panes are the same shape. Pinning at the right-hand edge is not
    specified.*
  - ***A pinned column covers the pixels underneath it***, and this is where the axes stop
    matching. The header occupies the content's first row height **and** covers the Viewport's
    first row height, so the two cancel and the vertical arithmetic is untouched. A Pinned Column
    covers the Viewport's left edge while occupying nothing ahead of the content, so it cancels
    nothing: a column scrolled to its own offset lands **underneath** the pinned block, in the
    DOM and unreadable, and columns wholly under it are left out of the slice for the same
    reason. `ScrollLeftToReveal` subtracts the pinned width in one place so nobody re-derives it.*
  - ***Auto widths are observed for every column, painted or not*** — the one thing that keeps
    the horizontal axis stable. Measuring only the painted columns would make a width depend on
    where the Viewport sits: a column entering from the right grows, the total width changes, the
    scrollbar changes length, and the same `scrollLeft` points somewhere else. Observing all of
    them takes the position out of the input and the loop cannot form. The cost is paid once per
    row, not once per frame: observation is a monotone maximum
    ([ADR-0016](./0016-column-width-and-overflow.md)), so only rows newly entering the Viewport
    are read.*
  - ***A fling is either axis*** — but the horizontal half only counts when the columns are
    virtualised. A diagonal gesture that crosses screens sideways while barely moving down
    changes every cell just the same; with virtualisation off nothing changes at all, and
    blanking the rows would be the only work in the frame, followed by rebuilding every cell
    when it settled. A fling is a way of not painting what would otherwise have to be painted,
    so where there is nothing to skip there is no fling. Pinned Columns are painted **through**
    one either way — they are the landmark that survives panning, and having them blink out and
    back is what pinning exists to prevent. Only the scrollable cells are skipped.*
  - ***The header moved inside the scroll container***, held with `position: sticky` beside the
    pinned cells. Two nested containers cannot do this: a non-visible overflow on one axis makes
    the other compute to `auto` (CSS Overflow 3), so the inner one becomes the scrollport, the
    body pans on a scrollbar the header does not follow, and every column is read under its
    neighbour's label. One container also removes that class of desynchronisation structurally.
    The header is exactly one row tall — see ADR-0013, and "Tiered headers" below.)*
- **Placeholders are the same mechanism as
  [ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) already needs.** With a
  server-side Consumer, rows for a distant Window have not been fetched at all and something must
  be shown while they are in flight. "Do not paint contents during fast scrolling" reuses that
  machinery rather than adding a concept. **Do not design fetch-waiting and render-throttling as
  two different things.**
- **What counts as "fast" becomes a tuning parameter.** Too low a threshold and contents blank out
  during ordinary scrolling; too high and it never engages. Decide by measurement.
  *(Refined while implementing: a fling is a move of more than one Viewport, on **either** axis,
  and Placeholders are filled in after 150ms of stillness. Both numbers are provisional and
  neither has been measured yet — they are recorded here so the next measurement knows what it is
  changing.)*

### Measured: horizontal virtualisation is on by default

`VirtualiseColumns` defaults to **on**, decided by measuring, not by reasoning — this project has
been wrong twice by reasoning (ADR-0003, ADR-0008). The sample is
`samples/ExGrid.DemoHost` `/wide`: 100,000 rows × 100 columns, 28px rows, a 900×600 Viewport, two
pinned columns, the same data with the setting toggled. Blazor WebAssembly, headless Chromium on
an M-series Mac, Debug build. **Not a gate** — this is an environment-dependent number kept to
justify a default.

| | on | off |
|---|---|---|
| Cells in the DOM | **220** | **2,200** |
| Elements under the instance root | 279 | 2,326 |
| **The repaint after a fling settles** (script + style + layout, median of 8) | **16.3 ms** (11–31) | **49.8 ms** (32–55) |
| Frame interval, ordinary scrolling, either axis (median of 150) | 8.3 ms | 8.3 ms |

Two things this says that were not obvious beforehand:

- **The settle frame is where the setting shows.** 2,200 cells cost 49.8 ms to build — the same
  region this ADR's original 2000-cell measurement found (25–33 ms on the spike's hardware), and
  three times a frame. 220 cells cost 16.3 ms. This is exactly the "single frame after scrolling
  stops" that made throttling-alone insufficient above, and horizontal virtualisation is what
  makes it affordable.
- **Ordinary scrolling keeps up either way, and unvirtualised is not slower — it does less.**
  With every column already in the DOM, panning sideways changes nothing and there is no render
  at all; virtualised, every row is rebuilt when the column set moves. Both stayed at the frame
  cadence, so the per-frame cost is not what decides this. **Had only the scrolling frames been
  measured, the conclusion would have been the wrong way round.**

**Off is still supported and travels the same rendering path**, for the grid narrow enough that
2,200 cells never arises, and for anyone who wants the whole row in the DOM (find-in-page reaches
what is painted, and nothing else).
- **The Consumer's column count becomes a specification concern.** 40×20 fits and 40×50 does not,
  so how many columns are placed side by side has a direct performance consequence. Horizontal
  virtualisation removes the ceiling on total columns, but **pinned columns cost directly**.
- **Paging removes this worst case entirely on the screens that use it** — a pager has no fling,
  so the full rebuild happens once per click and goes unnoticed
  ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)).

## Tiered headers — open

A header several rows deep, with a group label spanning a run of columns, is **not designed**.
It is recorded here rather than elsewhere because horizontal virtualisation is what it collides
with. What is already known:

- **The coordinate arithmetic carries over unchanged.** A group's left edge and width are
  `OffsetPxOf(first leaf)` and `OffsetPxOf(last leaf + 1) − OffsetPxOf(first leaf)`. No new
  arithmetic is needed.
- **But the header can no longer be built as a flow of cells.** With virtualisation on, a group
  spanning leaves 15-30 while leaves 20-28 are painted is only partly on screen, and the leaves
  carrying the rest of its width are not in the DOM to carry it. Group rows would have to be
  **absolutely positioned**, which means rebuilding the flex row of header cells this ADR's
  implementation settled on.
- **The header's depth becomes an input to the geometry.** The header is deliberately exactly one
  row tall, which is what lets it cancel against the Viewport's top edge and leave the row
  arithmetic alone ([ADR-0013](./0013-fixed-row-height.md), where the same note is left). A
  tiered header ends that simplification and changes what `ViewportHeight` means. **This is the
  decision that overturns it** — nothing else is expected to.
- **A group straddling the pinned boundary is undefined.** Half of a group sticky and half of it
  scrolling is not a thing that can be drawn. Refuse it, or split it visually — undecided.
- **The Column model stops being a flat list.** Column Group would be a new term for
  `CONTEXT.md`, column reordering (open — see
  [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) becomes
  constrained by groups, and whether a copy includes the group row is unsettled
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md) says a copy follows the current
  column order and excludes hidden columns, and says nothing about a header at all).
- **It does not affect the budget this ADR is about.** There are never more groups than painted
  leaves, so the cells-per-frame ceiling is unchanged.
