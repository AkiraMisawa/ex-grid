# A 3D surface takes a grid of values, and is turned, zoomed and sliced

*Decided with the user, 2026-10-19 (M2 grilling Q5 and Q6). The 3D entry is ADR-0255's twelfth.* *(The fourteenth of ADR-0021's list since
ADR-0240's two joined it, 2026-10-19.)*

## The data is a grid

**A surface is x values × y values × a table of z** — strikes × tenors × volatilities. A missing value
is a hole in the surface: never 0 and never filled from its neighbours. Scattered points are refused
by name. Joining them into triangles is interpolation, and ExChart computes none (ADR-0257): a
triangulation's choice changes the surface's shape, and a plausible surface the analytics never
produced is the quiet error spine rule 1 forbids.

## What the user does

- **Turn** it by dragging, or with the arrow keys.
- **Zoom and pan** it (the user chose this over leaving them out, 2026-10-19).
- **Read** a value: the tooltip names the grid point nearest the pointer — x, y and z, each through its
  format. It never reads a value between grid points.
- **Slice** it: a click on a tenor or a strike draws that slice — a smile, a term structure — as a
  2D chart beside the surface, where values are read exactly. Depth makes a 3D value hard to read; the
  slice is where it is read.

Angles, zoom and centre are raised and taken back as ADR-0272 says, so a redraw keeps the view.
