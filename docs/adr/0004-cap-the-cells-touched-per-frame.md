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
- **Placeholders are the same mechanism as
  [ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) already needs.** With a
  server-side Consumer, rows for a distant Window have not been fetched at all and something must
  be shown while they are in flight. "Do not paint contents during fast scrolling" reuses that
  machinery rather than adding a concept. **Do not design fetch-waiting and render-throttling as
  two different things.**
- **What counts as "fast" becomes a tuning parameter.** Too low a threshold and contents blank out
  during ordinary scrolling; too high and it never engages. Decide by measurement.
- **The Consumer's column count becomes a specification concern.** 40×20 fits and 40×50 does not,
  so how many columns are placed side by side has a direct performance consequence. Horizontal
  virtualisation removes the ceiling on total columns, but **pinned columns cost directly**.
- **Paging removes this worst case entirely on the screens that use it** — a pager has no fling,
  so the full rebuild happens once per click and goes unnoticed
  ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)).
