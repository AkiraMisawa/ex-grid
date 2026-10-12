# A range's tint is an element of its own, and on the Paper the pinned tints multiply as one layer

*(Decided with the user on 2026-10-11, when `0.1.0` was found to need DC-59 in a Pinned Column. It
amends [ADR-0008](./0008-selection-is-painted-by-an-overlay.md)'s range elements,
[ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)'s "That stays owed",
and [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)'s `ex-range`.)*

On a Sheet's Paper, a supplied Border line lies above the Selection's shade, which multiplies with it,
so a black line inside a selected range stays black, as in Excel (ADR-0071, DC-59, SH-54). In a Pinned
Column it does not, and ADR-0071 recorded that as owed. Read against the code (`afffa77c`):

- **`.ex-viewport` is one stacking context** (its inline `transform`). Inside it, the scrollable
  selection layer (`.ex-selection`, z 1; on the Paper z `auto`), the pinned selection layer
  (`.ex-selection-pinned`, sticky, z 3), the rows, and each pinned cell (sticky, z 2 — every pinned cell
  is a stacking context of its own; there is no pinned block element).
- **A range is one element per layer**, `.ex-range { isolation: isolate }`, its tint a `::before` at
  z −1, its single-range outline an `::after`. A range across the pinned boundary is drawn whole in both
  layers, each cut to its side by an inline `clip-path: inset(…)`.
- **On the Paper the scrollable tint multiplies** because ExSheet's stylesheet takes the scrollable
  layer's `z-index` and the range's isolation away there, so the tint's group is the Viewport, which holds
  the rows. **The pinned tint cannot**: it sits inside two isolated groups (its `.ex-range`, and the
  sticky z 3 layer), neither of which holds the pinned cells, and a blend sees only what its own group
  painted before it — against a transparent backdrop multiply is plain source-over.
- **Found while reading it: a range that crosses the pinned boundary does not multiply on its scrollable
  side either.** Its `.ex-range` carries the inline `clip-path`, and any `clip-path` makes a stacking
  context, which isolates the `::before`'s blend. On `/sheet` column A is pinned, so this is every
  whole-row selection, Ctrl+A, and any range that starts in A. (From the specifications and Blink's code;
  the pixel test below confirms or refutes it.)
- **What a user sees depends on the shade's colour.** Multiply and source-over differ by a·C·(B − 1) per
  channel, which is zero only for a black shade or a white backdrop. The built-in Chrome's black 22% gives
  the same pixels either way. ExSheet.MudBlazor's shade (the primary at 18%) turns a black line inside a
  pinned range into (16, 13, 41); a Consumer's coloured `--ex-selection-fill` does the same.
- **SH-54 was only ever checked where nothing is pinned** (case 11 of `part-c.spec.mjs`).

## The decision

### A range is a tint and a box

- **In each layer a range reaches, it is two elements**: a **tint**, which paints
  `--ex-selection-fill` with `--ex-range-hole` cut out, and a **box**, which keeps the class `ex-range`
  (ADR-0029's selection rectangle) and draws the outline.
  - The box is drawn whole and clipped to its side, as today, so the outline, the forced-colors outline
    and UX-19's no-seam rule are unchanged.
  - The tint is cut at the pinned boundary from the geometry the bands already use (`SelectionStyles`),
    and carries the hole and no other clip.
  - The range's isolation, its `::before` and the z −1 trick go: in each layer, tints come first by tree
    order, then boxes. The tint's class and the new layer's class are internal, not presentation
    surface.
- **The pinned tints get a layer of their own**, placed immediately before `.ex-selection-pinned`:
  sticky, z 3, no pointer events, hidden from assistive technology, keyed like its siblings, and emitted
  only while it holds something. It holds the pinned hover and Focus bands first (ADR-0008's "bands
  beneath every range" still holds), then the pinned tints. `.ex-selection-pinned` keeps everything drawn
  on top — boxes and outlines, the Focus, the Copied Range, the fill handle and its target, Reference
  Outlines, column outlines and Point.

### On the Paper, every wash multiplies

- **ExSheet's stylesheet puts `mix-blend-mode: multiply` on the pinned shade layer itself**, whose
  children are plain, and on each scrollable tint. A later sibling group in the Viewport blends with
  everything the Viewport painted before it, the pinned cells included — so the layer, not its contents,
  is what blends. The overrides that took isolation and `z-index` away go. *(As built, 2026-10-12: what
  went is the tint's `::before` and its `z-index`, and the range's `isolation`. The scrollable layer's
  `z-index: auto` on the Paper stays, since the tint's group is the Viewport only while that layer makes
  no stacking context of its own.)*
- **The bands multiply on the Paper too**, on both sides (decided with the user): "on the Paper every wash
  multiplies and a line stays as drawn". Over white nothing changes, and ExSheet turns on neither band
  itself.
- **The core's stylesheet gets no blend.** ExGrid and ExPivot paint the same pixels, in the same order —
  bands, tints, boxes — as before.
- **Under forced colors** the tints and the shade layer are not painted, stated rather than left to how
  a browser forces a translucent background. *(As built, 2026-10-12: no tint is painted and nothing
  blends, and the shade layer still holds the bands' forced-colors outlines on both sides, so a band
  keeps its dotted edge in forced colours as it did before this decision.)*

### What it costs, and what is checked

- **Per range, never per cell or per row**: at most four elements per range (a tint and a box in each
  layer) where there were two, and the one layer. A selection change renders the root and no row, so
  ADR-0008's "how many rows change membership at once" stays zero; P1–P9 are checked again (below).
- **The compositing cost is a prediction, not yet a measurement.** If Chrome composites the sticky pinned
  cells, a blend above them reads its backdrop from an offscreen surface, which costs a pass per frame
  and, at a fractional device-pixel offset, could blur lines the way ADR-0053's 2026-10-01 note removed.
  So:
  - **the sub-pixel test gates it**: with a pinned tint on screen and the page shifted by a third of a
    pixel, the lines outside the range keep their exact Device Pixel patterns at 100% and 150%. **If it
    fails, the work stops and comes back to the user as a decision** — one pinned block, or the pinned
    shade left as it was and DC-59 recorded as failing — never tuned offsets;
  - frame intervals while scrolling with column A selected, before and after, at 100% and 150%, and the
    composited layer count read through CDP, are recorded before this ADR counts as done.

### Verification

- **Layer 2**: a pinned-column twin of PF-5's element count and P3's render counts across a drag that
  crosses the boundary; the markup (tint in the shade layer, cut at the boundary, the hole where the
  Focus is, boxes keeping `inset(…)`, no layer for a one-cell selection, hidden from assistive
  technology); the core stylesheet holds no `mix-blend-mode` and no transition on the new classes; the
  per-cell allocation slope stays 0.
- **Layer 3**, in `part-c.spec.mjs`, named for DC-59 so that the `chrome-150` project runs it too: a range
  wholly pinned and one crossing the boundary under MudBlazor (black lines stay black within ±2); their
  built-in twins (#C7C7C7, black lines, the Focus unshaded); a Consumer's coloured token over a pinned
  Fill and a pinned line; the sub-pixel gate; scrolling with column A selected; forced colors and the
  dark scheme with a pinned range.
- **Performance**, observational: the drag cost before and after with
  `verification/2026-09-29-selection-hole/drag-cost.mjs`, extended for `pin` and `chrome`, on both hosts;
  the scroll frames and layers above; PF-6 and PF-7.

## Considered options

- **Paint the pinned tints in the scrollable layer, placed at the scroll offset by C#.** Every sideways
  scroll would need a render, and on Blazor Server the tint would trail its cells by a round trip
  (principle 6) — the drifting band ADR-0008 refused.
- **Make the pinned cells stop being stacking contexts.** They must be sticky, and they are not the
  obstacle.
- **A scroll-driven animation to move the tints.** An animation inside the Viewport (P8), for what sticky
  gives for nothing.
- **One pinned block**: every row's pinned cells in one sticky element, the shade inside it. Certain to
  blend without an offscreen surface, but it splits each row across two parents (ADR-0033's rows), adds a
  component per row, and re-homes the Row Headings, Placeholders, Interactive and Template cells and the
  pinned gridline rules. The fallback if the sub-pixel gate fails, not the plan.
- **Draw the pinned cells' lines above the shade**, per cell or as a Border overlay. An element per pinned
  cell, or a second way to paint lines beside ADR-0071's, and a Fill would still be tinted rather than
  darkened.
- **`backdrop-filter`**, or **an achromatic shade on the Paper**. The first can only multiply by grey; the
  second reverses ADR-0071's MudBlazor shade and leaves a Consumer's coloured token wrong in exactly this
  place.

## Consequences

- ADR-0008: a range is "a tint and a box per layer it reaches", not "at most two elements"; "drawn whole
  and clipped" is the box's; the range no longer isolates its own painting.
- ADR-0071: "That stays owed" and "over a Pinned Column, where it cannot multiply" are settled here, with
  the crossing-range defect and SH-54's unpinned-only check recorded.
- ADR-0029: `ex-range` names the box; `--ex-range-hole` is written on the tint.
- The Definition of Done: DC-59 and SH-54 hold in a Pinned Column and across the boundary; DOM-2's "one
  overlay element per range" becomes "a tint and a box per range in each layer it reaches"; PF-5's layer-2
  check is extended to pinned columns.
- The Docs Site's Sheet formatting page says that lines inside the Selection stay as drawn and its shade
  darkens beneath them, in a frozen column too. The README's budget recording is made again if a frame
  shows a range in column A under MudBlazor.
