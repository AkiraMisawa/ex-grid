# ExGrid is a desktop grid. Narrow means a smaller desktop, not a phone

*(Decided with the user, 2026-09-25, after comparing ExGrid with MudBlazor's `MudTable` and
`MudDataGrid`. Both switch to a stacked "small-device mode" below a `Breakpoint`, `Xs` by default.
The question was what this grid does instead.)*

Until now the answer was spread across three places, and one of them gave a reason that did not
hold:

- [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) put touch and pen out of scope in one
  sentence: "this is a desktop grid".
- [ADR-0017](./0017-target-chromium-browsers-only.md) put Safari out of scope. Every browser on
  iOS and iPadOS is built on WebKit, so that excluded the iPhone and the iPad as well, although
  nothing said so.
- [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) refused the
  Wrapper's `Breakpoint` with "no stacked layout, the grid is an island
  ([ADR-0031](./0031-the-grid-lays-out-left-to-right-only.md))". ADR-0031 is about right-to-left
  layout. The "island" there is a `dir="ltr"` attribute, and it says nothing about stacking.

This ADR is the one place the answer lives.

## Decision

**The grid targets a desktop: a keyboard, a mouse, and Chrome or Edge on Windows, macOS or
Linux.** Touch, pen, phones and the iPhone and iPad are out of scope. Android's Chrome is a
Chromium, so the grid will load there, and nothing is promised about how it behaves.

**"Responsive" here means following the box.** A desktop window is resized, a Drawer opens, a
split pane moves. The grid follows its box through `ViewportSize.Fill`, reported by the browser
([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)), and nothing else
about it changes with the width. Column widths are not squeezed to fit. When the columns are
wider than the box, a horizontal scrollbar appears, as in Excel. Density is the Consumer's
choice, and the grid does not switch it by width.

**Columns narrower than the box are not stretched.** The space to their right stays empty, as an
empty sheet does in Excel. Stretching them, as `MudDataGrid` does, would make a column's width
depend on the window. A number readable at one window size would then show `####` at another,
and the width painted would stop being the width recorded in View State
([ADR-0016](./0016-column-width-and-overflow.md)).

What a resize does to the rest is decided where each thing lives: the Focus is not chased
(ADR-0012), a popover with no room closes
([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)), and the first frame after a
change is painted from the previous size (ADR-0028). On a Server circuit that first frame lasts a
round trip rather than a frame. How long, during a window drag, has not been measured. It is
listed with the other unmeasured numbers in the Definition of Done.

**There is no stacked layout, and the Wrapper keeps refusing `Breakpoint`.** Every rule this
grid is built on is defined on a two-dimensional grid of cells: selection as rectangles in index
space ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
the painted Overlay (ADR-0008), what an arrow key means
([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)), the shape of a copy and a paste
([ADR-0005](./0005-copy-refuses-rather-than-truncates.md),
[ADR-0014](./0014-paste-shape-rules-and-selection-count.md)), the fixed row height
([ADR-0013](./0013-fixed-row-height.md)), Pinned Columns and the header. A card per row keeps
none of them. Supporting one would mean defining every one of those rules a second time, for a
layout that Excel itself does not use: its mobile apps keep the grid. A Consumer who wants cards
below a width swaps in a different component at a breakpoint of its own choosing. `MudDataGrid`
itself is a reasonable choice there.

**Pinning is suspended while it would leave nothing to scroll.** On a narrow box the Pinned
block can cover the whole Viewport. The rows then show only pinned columns, panning moves
nothing visible, and a Focus moved into a scrollable column stands underneath the pinned block,
which breaks ADR-0012's "the Focus must always be visible" without anything looking wrong. So
while the band the Pinned block leaves for the scrollable columns is narrower than the default
`MinWidth` (40px), **every column scrolls together**, as if nothing were pinned. When the box
widens again, pinning comes back. This changes how the grid is painted, not what was asked for:
the pinned count in View State is untouched, so nothing is written back to a saved view. Before
this, `ColumnGeometry` called the covered case "legal — the user sees only pinned columns" in a
comment, and nothing else decided it.

## Rejected

- **A stacked layout in the core.** See above: it is a second component sharing a name with the
  first.
- **Touch as a first-class input** (read-only display on tablets, or full touch gestures). A
  touch drag is a different gesture vocabulary, not the same vocabulary with a different device
  (ADR-0008). The product's claim is Excel's keyboard and mouse operability, and that is where
  the effort goes.
- **Leaving a covered Viewport as it was**, which is also what Excel does with frozen panes wider
  than the window. It fails quietly: the Focus is somewhere the reader cannot see. The rule in
  this repository is to say so or show it, never to leave something that looks right and is not.

## Consequences

- **ADR-0030's reason for refusing `Breakpoint` now points here.** ADR-0008's sentence on touch
  stands, and points here as well.
- **Suspended pinning is a geometry fact, decided in `ColumnGeometry`**, not in the stylesheet:
  the sticky offsets, `ScrollLeftToReveal` and the horizontal slice all have to agree on it, as
  they already agree on the pinned width.
- **Layer 3 can check it** on the Linux runner by narrowing the page below the pinned width and
  arrowing into a scrollable column.
