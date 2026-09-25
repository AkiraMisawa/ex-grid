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
  *(Refined while implementing: it shares the translation by living **inside** the translated
  element, and that costs the second half of the sentence. The row Viewport carries a `transform`,
  which makes it a stacking context, and the Pinned Columns' `z-index` therefore counts only
  inside it — an overlay placed outside would paint **over** the pinned block instead of sliding
  beneath it, dragging a translucent band across the one part of the grid that is supposed to stay
  still. So the rectangles are positioned against the painted slice and move when it does. It is a
  handful of numbers per scroll against a visible defect, and the property that actually matters —
  that the cost is per range and not per cell — is untouched.)*
- **It has to mesh with horizontal virtualisation.** Once columns are virtualised the x
  coordinate comes from cumulative column widths
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)). That arithmetic is needed for layout
  anyway, so it is not an added cost, but the implementations are coupled.
- **A range crossing the pinned boundary is two rectangles** *(added while implementing)*. The
  same asymmetry the rest of the horizontal axis has (ADR-0004): the pinned part has to travel
  with the Pinned Columns while the rest pans away underneath, so there are two layers — one
  positioned in the content, one held against the Viewport's left edge — and a range that spans
  the boundary contributes to both. Disjoint ranges therefore cost at most two elements each
  rather than one, which changes nothing about the argument above.
- **Which cell the pointer is on is arithmetic, not a measurement** *(added while implementing)*.
  Every cell is `pointer-events: none`, which is inherited, so the row Viewport is the target of
  every mouse event in the body and the offsets on the event are already relative to it. The grid
  then asks `ViewportGeometry.RowAt` and `ColumnGeometry.ColumnAt` — the same numbers that painted
  the cell — instead of measuring elements, which would be the layout read
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) refuses. One handler for the whole
  body rather than one per cell also keeps the ~220 painted cells free of handlers that would be
  re-registered on every frame of a scroll (ADR-0004). **The move handler is attached only while
  a drag is running**, because a pointer merely crossing the grid would otherwise raise an event
  per frame — for a Blazor Server Consumer, a wire round trip each time.
- **800 cells was not measured on real hardware.** Only 2000 cells (40×50) was confirmed there.
  With horizontal virtualisation
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)) the effective cell count drops to
  around 800, and the tail at that size has not been measured on real hardware. Since the overlay
  does not depend on cell count, the conclusion is not expected to change.

## What the mouse does not do yet — two things, both with named triggers

The gestures ADR-0012 names are wired; two things around them are deliberately not, and the third
— dragging past an edge — is decided below.

- **The fill handle is not painted.** This ADR names it as one of the three things an overlay
  draws, but what it does belongs with edits
  ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)) and neither the gesture nor
  the fill semantics are settled.
- **Right-click and double-click leave the selection alone.** A secondary click is ignored rather
  than guessed at: in Excel it moves the selection unless the cell is already inside it, and a
  context menu is a Chrome seam (ADR-0010) that has not been specified. Double-click starts editing
  and arrives with the editor.

Touch and pen are out of scope for the same reason the browser target is narrow
([ADR-0017](./0017-target-chromium-browsers-only.md)): this is a desktop grid, and a touch drag is
a different gesture vocabulary rather than the same one with a different device. *(The
desktop scope as a whole is now stated in one place:
[ADR-0043](./0043-exgrid-is-a-desktop-grid.md).)*

## Dragging at the edge scrolls, and the pointer leaving stops it

*(Decided after the keyboard shipped; this section replaces the "no decision about the rate, and
about what happens when the pointer leaves" bullet above.)*

**An edge band, and a rate that grows with how far into it the pointer is.** The band is the
Viewport's inner 20px on each scrolling axis; a drag whose pointer sits inside it scrolls by one
row per tick at the band's inner lip, rising to eight at the outer edge, and the selection extends
to whatever cell the arithmetic then puts under the pointer. The tick runs off the injected
`TimeProvider` the settle delay already uses, so the whole rate curve is pinned in layer 2 with a
`FakeTimeProvider` and no browser.

**The ceiling is not a taste.** It is one row short of
[ADR-0004](./0004-cap-the-cells-touched-per-frame.md)'s fling threshold: scroll a full Viewport in
a tick and the rows become Placeholders, so the user would be dragging a selection across rows
whose values have not arrived. Selection is index space and would survive that
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) — but
being shown a blank grid while choosing what to select is the kind of quiet wrongness this project
refuses, and the bound falls out of a decision already taken rather than being guessed.

### The half that was actually hard: no events arrive from outside the element

Auto-scroll has to be timer-driven, because a pointer held **still** inside the band must keep
scrolling and a still pointer raises no events. That is what makes leaving the element dangerous:
outside it there are no `mousemove`s at all, so a running timer learns neither "still held" nor
"released" — and a drag released outside would scroll to the end of a million rows with nothing to
stop it.

**`@onmouseleave` stops the timer.** It is an ordinary Blazor event on the instance root, it fires
exactly when the events stop arriving, and it costs nothing. The drag itself is left alone: the
`e.Buttons` check on the next move already ends it if the button came up while the pointer was
away, which is the mechanism the mouse handler was built on.

Rejected on the way here:

- **`setPointerCapture`.** It is what Excel's behaviour really wants — events keep arriving outside
  the window, and the band could go away entirely. Blazor has no API for it, so it would be a
  **fifth entry on ADR-0021's allowlist** and its own ADR. Bought for a gesture the edge band
  already serves, with keyboard extension reaching the same cells either way.
- **A document-level listener.** Refused for the reason
  [ADR-0018](./0018-multiple-instances-must-be-independent.md) exists: every grid on the page would
  react to every drag.
- **A timer that keeps running after the pointer leaves**, at the last known rate, until something
  contradicts it. There is nothing to contradict it, which is the whole point.

**The three numbers — the 20px band, one row and eight rows per tick — are provisional and
unmeasured**, exactly like ADR-0004's fling threshold and settle delay, and are recorded as
OBSERVATIONAL rather than gated. What is *not* provisional is the shape: a band, a rate that grows
inside it, a ceiling under the fling threshold, and a stop on leaving.

## The Focus band *(added while designing the presentation contract)*

A translucent band across the full width of the row the Focus is in — what Excel 365 ships as
"Focus Cell", and the answer to "which row am I on?" in a grid a hundred columns wide. It is
specified here because it is **one more overlay rectangle and nothing else**: this ADR's economy
(cost per rectangle, never per cell) prices it at O(1), it crosses the pinned boundary as two
layers exactly as every other rectangle does, and no row learns anything — the band moves when
the Focus moves and the rows keep skipping (ADR-0003).

- **Off by default**, as Excel's own Focus Cell is; enabled by a C# parameter
  (`HighlightFocusRow`) — whether it is on is behaviour, its colour is appearance, which is
  precisely [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)'s split.
  The fill is a Visual Token (`--ex-focus-row-fill`,
  [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)).
- **A column band is the same mechanism turned sideways** and is reserved, not specified.
- **Zebra striping was refused in its favour**
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)): stripes would
  need an absolute row index threaded through every row, answer a different question ("is this
  one row or two?"), and answer this one — where is the Focus — not at all.
- Painted beneath the selection rectangles: a range must stay readable over the band.

Implemented as specified: `HighlightFocusRow` on the component, one rectangle per layer
painted first so every range stays readable over it, and `--ex-focus-row-fill` as the colour.
