# A chart's tooltip shows every series at the pointer's position

*Decided with the user, 2026-10-19 (M2 grilling Q1).*

On the exposure page, EPE, PFE95 and PFE99 share one axis, and the reader's question at 2027-03-31
is how the three compare there. A tooltip for the nearest series alone answers another question, and
where lines cross or lie close, which series it shows depends on a pixel of pointer movement.

**The tooltip shows every visible series of the panel the pointer is in, at the position the pointer
is over** (ADR-0255 reports it):

- the position (a date, a number or a category), then one line per series: its name, its Appearance
  Keys' swatch (ADR-0256) and its value written as ADR-0251 says, through the column's own format;
- **many series**: the N nearest the pointer's height, then "and M more series" — never a tooltip
  taller than the plot, and never a silent cut;
- **panels split**: only the pointer's panel is listed; the vertical guide is drawn across every
  panel, so the reader can follow the same date down;
- a thinned pixel names its range (ADR-0254), a step names its interval (ADR-0253), an interpolated
  value says so (ADR-0257).

The tooltip is its own component (ADR-0252): a move to another position renders it and the guide,
nothing else.

## Considered

- **The nearest series only** (a default of several libraries): rejected, for the reason above.
- **A switch between the two**: rejected; nobody asked for the nearest-only mode, and it is a
  parameter to document and test for no reader.
