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
  looks like. *(Of the range, and not of the active cell, which Excel leaves untinted. Corrected
  2026-09-29: see "Excel's look for the Focus and a single range" below.)*
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
  the fill semantics are settled. *(Settled 2026-09-27 by
  [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md): the core paints the handle and owns
  the drag, and raises a Fill Intent; the Consumer decides what a fill means.)*
- **Right-click and double-click leave the selection alone.** A secondary click is ignored rather
  than guessed at: in Excel it moves the selection unless the cell is already inside it, and a
  context menu is a Chrome seam (ADR-0010) that has not been specified. Double-click starts editing
  and arrives with the editor.

Touch and pen are out of scope for the same reason the browser target is narrow
([ADR-0017](./0017-target-chromium-browsers-only.md)): this is a desktop grid, and a touch drag is
a different gesture vocabulary rather than the same one with a different device. *(The
desktop scope as a whole is now stated in one place:
[ADR-0045](./0045-exgrid-is-a-desktop-grid.md).)*

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

## Excel's look for the Focus and a single range *(decided with the user, 2026-09-29)*

Seen on ExSheet's demo page, the active cell's outline was thinner on one side than on the other
three, and a selected range had no outline of its own. Both are corrected here, for ExGrid as a
whole, since the Focus is Excel's active cell everywhere
([ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)).

- **The Focus outline is drawn inside its cell.** It was a 2px outline offset by −1px, so it
  straddled the cell's edge. The outer pixel went beneath whatever is painted above the selection
  layer: a Pinned Column beside the first scrollable column, the Headings beside column A
  ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)), and the header above row 1. Measured
  in device pixels on 2026-09-29, those edges were 1px and the others 2px. Lifting the outline
  above those layers is not an option: a cell scrolled beneath the pinned block has to go beneath
  it, outline and all. So the whole width now lies inside the cell, as the pointing outline and the
  chosen action already do, and nothing above the layer can cover part of it. The fill handle
  stays centred on the outline's corner. The forced-colors outline of a range is drawn the same
  way.
  *(2026-10-01, decided with the user, ticket 49: outside where nothing covers it.)*
  - Excel draws the Selection's outline on the gridline and one pixel outside the range, so it
    covers a Border on all four outer edges (the eleventh Windows run, case 11). Drawn inside, ours
    covered the bottom and right lines but left the top and left ones showing just outside it,
    1 to 2 px from Excel's picture.
  - So the outline lies as Excel's does, on the gridline and one pixel out, on every side.
  - Only where something above the selection layer would cover that outer pixel does a side stay
    inside, as above, so that its width stays equal (UX-18). This originally included the Headings,
    a Pinned Column and the header.
  - *(2026-10-14, decided with the user, D2: the Heading Mark stands for a covered side.)* Where
    the header or Row Headings cover the side of a Focus or single range, the side stays outside,
    beneath that band: the Heading Mark supplies its two Device Pixels, rather than two lines
    standing beside each other. A side against the pinned block, or the grid's edge with no Heading
    there, still stays inside: no Heading Mark stands there to replace it. This replaces that part
    of the 2026-10-01 rule; R5 (2) pictured one line of two Device Pixels at the Headings.
- **The Focus cell is not tinted, and a selection of one cell is not tinted at all.** The
  Consequences above say "this is also what Excel looks like" of the translucent fill. That holds
  for the range and not for the active cell: Excel leaves the active cell untinted inside a tinted
  range, and tints nothing when one cell is selected. A range that holds the Focus is therefore
  painted with a hole where the Focus is. **The hole is geometry**, resolved in C# with the
  rectangle it belongs to and emitted inline
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). The range stays one
  element, so the argument above (cost per range, never per cell) is untouched. That the hole adds
  nothing measurable to a drag is a claim, so it is measured before it is made.
- **A selection of one range carries one outline, around the whole range**, in the Focus outline's
  colour and width and drawn inside the range in the same way. The Focus inside it has no outline
  of its own and is marked only by being untinted. A selection of one cell is the case where the
  range and the Focus coincide, and it shows the Focus outline alone. *(Read, not observed: that
  Excel draws no separate outline around the active cell inside a single range. It is on the next
  Windows run's list.)*
- **Several ranges: no outline, no fill handle, and a thin border on the Focus** *(2026-10-12,
  decided with the user: in a Selection of several ranges ExGrid as a whole follows Excel, with no
  range outline and no fill handle, and the Focus cell takes a thin border of one Device Pixel in a
  Visual Token of its own, `--ex-focus-among-ranges-outline`; a single range keeps the look
  above)*. The decision followed the seventeenth Windows run's h2
  (`verification/2026-10-11-windows-17/report.md`, Part H). This bullet first left several ranges
  as they were, each tinted and the Focus outlined, until Excel had been observed.
  - No range carries an outline, and no fill handle is shown
    ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 5). Each range is tinted, with
    the hole where the Focus is, as before.
  - The border's colour *(2026-10-12, decided with the user: where nothing names
    `--ex-focus-among-ranges-outline`, the border is the Focus outline's colour,
    `--ex-focus-outline`, so a Theme or a Wrapper that colours the Focus colours it too, with no
    mapping of its own, and under the MudBlazor Wrappers it is their primary; on a Sheet's Paper it
    is `#217346`)*. Earlier the same day it had been decided as `CanvasText`, and black on the
    Paper, from h2's report. The readings of 2026-10-12 then measured Excel's border as `#217346`
    (R4): h2's black was its picture read at 1×.
  - It lies inside the cell, one Device Pixel in from its top and left edges and from the gridline
    the cell holds on its right and bottom, with the cell's own ground between it and the
    gridline. That is where the readings of 2026-10-12 measured Excel's active cell among several
    areas (R4), at Excel's 100% and 150% zoom on a 150% display, in each of three selections: one
    device pixel of `#217346` on every edge, one device pixel inside the cell, and white between it
    and the gridline. Nothing above the selection layer covers a side of it (UX-18), and every side
    lies on Device Pixels wherever the cell's edges do
    ([ADR-0090](./0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md)).
  - The core marks the Focus among several ranges with a class, decided from the Selection: a
    layer may hold the Focus and no range's box, so the stylesheet cannot tell from the Focus's
    siblings.
  - Under forced colors the border is not drawn, and the Focus is outlined there as it is
    everywhere else under forced colors, beside ranges that are each outlined.
  - *Not observed:* a display at 100% (R4 read the 150% display at both zooms, so one device pixel
    at a 100% display is by generalisation); a cell with a Fill (whether the line between the
    border and the gridline is the Fill, as built, or white); and the active cell while it is
    edited (ExGrid's Cell Editor covers the cell, and the border inside it).
    *(2026-10-14, after run 18a: a cell with a Fill.)* Run 18a filled D4 yellow before R4's first
    selection and read the four lines again (`verification/2026-10-14-windows-readings-18a/report.md`,
    `focus-among-ranges.md` 2; Excel's 100% and 150% zoom on a 150% display). The border is one device
    pixel of `#217346`, as before. Between it and the gridline lies a white device pixel on the left and
    the top, and the Fill on the right and the bottom: the left and top pixel is a line of its own, and
    the right and bottom pixel is the cell's ground. The Paper had the Fill on all four sides, and now
    draws the white line: on a Sheet, a Device Pixel of `--ex-sheet-selection-ring` outside the border's
    top and left. The gridline's own pixel read `#C7C700`, the Fill under the shade: the line recorded
    below as observed and not followed. The core grid keeps the cell's ground on every side: nothing on
    its pages was observed against Excel's Fill.
    *(2026-10-14, after run 18a: the active cell while it is edited.)* After F2 on D4, Excel draws no
    border on any edge at either zoom. The `#AFAFAF` line round the area stays, and the cell is white
    with the caret at its left (`focus-among-ranges.md` 3). On a Sheet the Cell Editor covers the cell,
    and with it the border and its white line. *(2026-10-16, decided with the user)* The editor itself
    draws no outline there while the keyboard is in the Sheet, and 1px while it is elsewhere
    ([ADR-0018](./0018-multiple-instances-on-one-page.md) section 6, ED-27), so an edited cell on a
    Sheet shows no border, as Excel's does.
  - *Observed and not followed* *(2026-10-13, decided with the user)*: R4's pictures frame each area
    among several ranges, a line of `#AFAFAF` on the gridline round it and a white Device Pixel
    inside it, and show the active cell's gridline as `#AFAFAF` on every side. The Paper draws
    neither. Whether that line is the gridline under the shade or a line drawn over it is
    unobserved: no run has looked with the gridlines hidden, and the next one does.
    *(2026-10-14, after run 18a: the gridlines hidden.)* With View, Gridlines off, in R4's three
    selections at both zooms, the `#AFAFAF` line is gone. That pixel is the shade, `#C7C7C7`, inside or
    beside a shaded area, and white outside one; the white device pixel between the border and that
    pixel stays (`focus-among-ranges.md` 4). So the line is the gridline under the shade, not a line
    Excel draws. As decided with the user on 2026-10-13, the Paper draws neither: nothing is built.
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
    ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)), and the line costs a box per run in each
    band. D1 below adds only a fixed marker per Row Heading in the painted Window.
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
  - *The shade, letters and corner* *(2026-10-14, decided with the user, D1: build all three).* R4
    and R5 read each marked Heading shaded, `#E1E1E1` where its column or row holds part of the
    Selection and `#D3F0E0` where the Selection holds the whole of it (from `#F0F0F0`), with its letters
    in `#217346` (from `#444444`), and the corner's triangle in `#217346` while every cell is selected
    (R5 (1)). These are the Sheet's light-scheme defaults. A Theme's tokens win, the bare grid keeps
    system-colour defaults (ADR-0029), and a MudBlazor Wrapper uses its palette (ADR-0030). Headings
    still follow the colour scheme, as ADR-0071 requires; Excel's dark Heading shades remain a reading.
    The whole-axis shade means every cell is covered by the Selection's union, including coverage by
    several overlapping ranges, never the sum of their sizes.
  - *How the letters are painted* *(implementation, 2026-10-15).* Header cells already belong to
    the root, so their shade and ink use interned classes. Beside each Row Heading's painted row,
    the root keeps one hidden `aria-hidden` state marker, whether that row is selected or not.
    A CSS sibling condition paints only the original Heading's shade and ink from that marker.
    No row receives a Selection parameter or renders for a Selection change; no cell's text is
    recoloured. The markers are bounded by the painted Window and their count is independent of
    selection size (DOM-2). The line stays one overlay rectangle per marked run.
    Repeating Heading text in an overlay was tried and replaced: it lost Consumer styling of the
    original stable row/Heading classes, and conditionally emitted labels grew with selected rows.
    Painting the original Heading retains all its typography, including Group/Total weight tokens
    and direct stable-class rules, without reevaluating Row Kind when its row skips rendering.
    All products' ordinary Focus and single-range borders
    default to two Device Pixels, so their other sides match the replacing mark at every scale;
    the former bare-grid default was two CSS pixels. Under forced colours, a visible border is
    restored on every Focus, including one among ranges, inside its cell except for a Heading-covered
    side; single-range borders follow the same rule. Multiple ranges keep their inside outlines.
    This applies D2 without losing the Focus when forced colours remove the tint.
  - *Where it meets an outline* *(2026-10-14, decided with the user, D2)*: the mark replaces a side
    covered by a header or Row Headings, as the revised 2026-10-01 bullet above states. The earlier
    implementation drew both lines and is replaced; the thin Focus border among several ranges is
    inside its cell and is unchanged by this decision.
  - *Not observed:* one cell and one ordinary range; a display at 100%; where a row's line meets a
    column's under the corner (ExGrid's stop at the corner, which stands above them); frozen panes. On
    the eighteenth Windows run's list (`docs/specs/exsheet/run-18/heading-mark.md`).
- **The range outline has the token ADR-0029 reserved for it.** `--ex-selection-outline` was
  reserved there as "the border Excel draws around the range's perimeter", and this is that border.
  Its default is `--ex-focus-outline`, so a Theme or Wrapper that sets only the Focus outline's
  colour gets both in that colour. *(Corrected the same day. This bullet first said the range
  outline read `--ex-focus-outline` and that no token was added, overlooking the reservation.)*

*(Settled while building, the same day.)*

- **A range across the pinned boundary is drawn whole in both layers, each clipped to its own side.**
  *(2026-10-11, [ADR-0206](./0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): that is now the range's box. Its tint is an element of its own, cut at the
  boundary, and the pinned tints and bands lie in a shade layer of their own; a range costs a tint and a
  box per layer it reaches.)*
  The earlier "two rectangles" bullet cut the range at the boundary. Cut that way, an outline drawn
  inside each half would draw a line down the boundary. Drawn whole and clipped, each half shows
  only its own side of one outline, so no seam shows, and C# never needs the outline's width.
- **The hole clips the tint only.** It is applied to the range's tint, never to its box, so the
  outline around a single range is never cut where the Focus is.
- **Under forced colors, the Focus inside a single range takes its outline back.** Forced colors
  discard the tint, and the missing tint is the only thing that marks that Focus. Without its
  outline, the Focus would not be visible.
- **The fill handle stays where it was**, centred on the range's corner, which is now the outline's
  outer corner. Centring it on the outline's centre line would need the outline's width in C#.
  That is a stylesheet value, and pairing it with a C# constant is the defect
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) removes.
  *(2026-10-12, decided with the user: whole columns put it at the first row's top-right corner and
  whole rows at the first column's bottom-left, as Excel does.)* There it lies just inside the
  cells, beside the Heading, which stands above this layer; Excel's straddles the Heading's edge
  (the readings of 2026-10-12, R5). Every cell has none, and a whole column's handle stays at the
  top of the part in view. [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), "The handle on
  a whole axis".
- **The hole was measured before it was claimed.** Main-thread time per ten-render selecting drag
  on `/sheet`, headless Chrome on an M4 Pro. With the hole switched off on the changed build, a drag
  moved by 0.6 to 0.7 ms (Server 16.2 against 15.6, WebAssembly 33.3 against 32.6). That is inside
  the 5 to 8 ms interquartile spread of the repeated before-and-after comparison. Style
  recalculation rose by 0.1 to 0.5 ms per drag. Nothing attributable to the hole was measured. The
  figures are medians whose per-drag rows were not kept. The script that writes the rows, and the
  pending re-run on a quiet machine, are in `verification/2026-09-29-selection-hole/`.

*(2026-10-02, [ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)'s
"What was decided after Part C".)* **On a Sheet's Paper the Selection's shade multiplies with the cells
beneath it**, so the lines inside a range stay as drawn, as Excel's do. It is still one overlay per range,
and still never a class on a cell: the shade is the range's own layer, painted with `mix-blend-mode:
multiply`, and the selection layer stops being a stacking context of its own there so that the blend
reaches the cells. The outline and the Focus's are not blended. Over a Pinned Column the pinned layer
is a stacking context above the pinned cells, so the shade cannot reach them, and there it lies over
the lines as before. ExGrid's own selection is unchanged.
