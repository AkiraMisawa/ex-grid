# Two entries join the JavaScript allowlist: the chart's pointer, and the 3D surface

*Decided with the user, 2026-10-19. The user allowed both (U4 and U15, 2026-10-13; confirmed 2026-10-19). This
rewrites ADR-0021's list from ten entries to twelve.*

## Eleventh: the chart reports the position under the pointer when it changes

ADR-0029's shape, on a chart's plot: one `mousemove` listener per chart instance reports the x
position the pointer is over **only when it moves onto another position**, and when it leaves. It
never measures: the positions' pixel bounds are computed in C# and handed to the module with each
drawing, with the drawing's version. A report taken against an earlier version is dropped (spine
rule 6). Blazor's `@onmousemove` would cross to .NET on every event, a round trip each on Server.
The proof of concept measured 48 crossings for 89 mousemoves over 44 dates.

## Twelfth: the 3D surface is projected and drawn in the browser while it is turned

Turning a surface follows the pointer at the frame rate; a round trip per frame cannot. .NET decides
the mesh, its colour scale and its ticks, and hands them over whole. The module only projects,
orders the faces far to near, fits the drawing to its box, and draws it as SVG elements of its own
box. It reports the angles when a drag ends, so the view is the Consumer's state and a redraw keeps
it. Colours are Visual Tokens the module reads from the stylesheet; it formats no number.

The first version uses it for the FX volatility surface (the user, 2026-10-19).
