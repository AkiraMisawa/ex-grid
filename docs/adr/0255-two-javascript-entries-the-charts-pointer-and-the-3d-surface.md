# Two entries join the JavaScript allowlist: the chart's pointer, and the 3D surface

*Decided with the user, 2026-10-19. The user allowed both (U4 and U15, 2026-10-13; confirmed 2026-10-19). This
rewrites ADR-0021's list from ten entries to twelve.* *(2026-10-19: ADR-0240's two entries, decided on
2026-10-18 in another branch, reached `main` after these, so the list holds fourteen and these two are its
thirteenth and fourteenth. The headings below keep the numbers they were decided with.)*

## Eleventh: the chart reports the position under the pointer when it changes

ADR-0029's shape, on a chart's plot: one `mousemove` listener per chart instance reports the x
position the pointer is over **only when it moves onto another position**, and when it leaves. It
never measures: the positions' pixel bounds are computed in C# and handed to the module with each
drawing, with the drawing's version. A report taken against an earlier version is dropped (spine
rule 6). Blazor's `@onmousemove` would cross to .NET on every event, a round trip each on Server.
The proof of concept measured 48 crossings for 89 mousemoves over 44 dates.

*2026-10-19 (M2 grilling Q10, ADR-0273).* A drag across the plot to narrow the range uses the same
reports — the position under the pointer when it changes, plus the press and the release — with no
listener added and nothing measured.

## Twelfth: the 3D surface is projected and drawn in the browser while it is turned

Turning a surface follows the pointer at the frame rate; a round trip per frame cannot. .NET decides
the mesh, its colour scale and its ticks, and hands them over whole. The module only projects,
orders the faces far to near, fits the drawing to its box, and draws it as SVG elements of its own
box. It reports the angles when a drag ends, so the view is the Consumer's state and a redraw keeps
it.

*Widened 2026-10-19 (M2 grilling Q6, ADR-0274).* The same module also **zooms and pans** the surface,
reporting zoom and centre when the gesture ends as it reports the angles; and, because only it knows
where each face was projected, it reports **which grid point the pointer is nearest** (for the
tooltip, when it changes) and **which tenor or strike a click fell on** (for the slice). It reports
grid indices into the mesh it was handed, with the mesh's version; .NET turns them into values. It
still formats no number and measures nothing. Colours are Visual Tokens the module reads from the stylesheet; it formats no number.

The first version uses it for the FX volatility surface (the user, 2026-10-19).
