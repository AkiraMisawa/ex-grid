# The grid lays out left-to-right only. RTL content is displayed; RTL layout is refused

The horizontal geometry is written in physical directions throughout — Pinned Columns stick to
the **left** edge, `ScrollLeftToReveal` subtracts a **left**-pinned width, numerics align
**right** ([ADR-0016](./0016-column-width-and-overflow.md)), and the selection overlay splits
into a left-held layer and a panning one
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). That is **deliberate, not an
oversight to be fixed with logical properties**: the grid supports left-to-right layout only.

The instance root carries `dir="ltr"` explicitly, so a grid embedded in an RTL page is an LTR
island rather than a half-mirrored one — the alternative is the failure this repository refuses
by principle: an ancestor's `dir="rtl"` would flip text flow while every offset the arithmetic
computes stayed physical, and the selection would drift off its cells **quietly**. Refusing
loudly is not available (there is no cheap detection worth adding); pinning the direction is,
and costs one attribute.

Two different things, only one of them refused:

| | Supported? |
|---|---|
| **RTL content** — an Arabic or Hebrew value in a cell | **Yes.** The Unicode bidi algorithm renders it correctly inside an LTR context, as Excel does on an LTR sheet |
| **RTL layout** — columns flowing right-to-left, pinning at the right edge | **No** |

## Why refuse rather than build it

- The target is line-of-business desktop screens for the first Consumer's domain; no RTL
  requirement exists or is foreseen. Building the mirror now would be designing against an
  imagined user — the kind of reasoning-without-measurement this project has been wrong with
  twice ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md),
  [ADR-0008](./0008-selection-is-painted-by-an-overlay.md)).
- Narrowing the target to buy capability is this design's established shape
  ([ADR-0017](./0017-target-chromium-browsers-only.md) did it for browsers).
- A design-system Wrapper on an RTL-capable system (MudBlazor ships RTL) leaves the grid LTR
  inside its RTL page and must not try to flip it
  ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).

## If real demand ever arrives

This ADR is revisited whole, as a geometry project, and the bill is known: mirroring
`ColumnGeometry` and the reveal arithmetic, the pinned edge, the gap spacer, both selection
layers, the numeric alignment, and the layer-3 assertions — plus an RTL machine to verify on.
It cannot be added as CSS.
