# A chart renders as SVG elements, in components that skip their render

*Decided with the user, 2026-10-19. Asked by the user the same day ("can SVG be diffed?"). The measurements are the
proof of concept's (research note §5).*

The proof of concept wrote its drawing as one string and rendered it with `MarkupString`. Blazor
cannot diff inside a `MarkupString`: every redraw resent the whole drawing — 428 KB at 67 dates,
61 MB at 10,959 — and a hover, which changes one tooltip, re-rendered all of it.

**ExChart renders the Engine's scene as SVG elements through the render tree**, which Blazor diffs
as it diffs HTML:

- **One component per part that changes on its own**: each panel's axes and grid lines, each
  series, the legend, the tooltip, the hover guide. Each writes `ShouldRender()` by hand
  (CLAUDE.md, ADR-0003) and compares the immutable piece of the scene it was given.
- **A series is one `<path d="…">`** (and its markers). When it changes, its `d` is sent whole; when
  it does not, its component does not render. Hiding one series from the legend sends one element.
- **The tooltip is its own component.** A hover re-renders it and nothing else.
- **Files are written when asked for.** SVG, PNG and CSV are produced on the click, not encoded into
  `data:` URLs on every render (more than half of the 67-date redraw).
- **No hover layer of dates × series.** The guide and dots are drawn for the one date the pointer is
  at (ADR-0255 reports it), not prewritten for every date (1,618 of 1,875 nodes at 67 dates).
- **`MarkupString` is not used for drawing.**

Bounded by decimation (ADR-0254), a series' path holds about two points per pixel of the plot's
width whatever the row count.

## Measured, not gated

The proof of concept's harness is rerun against the product on both hosts at 67, 1,566 and 10,959
dates, and the readings are recorded beside the proof of concept's. Targets are set from those
readings with the user; like every performance figure here they never gate CI.
