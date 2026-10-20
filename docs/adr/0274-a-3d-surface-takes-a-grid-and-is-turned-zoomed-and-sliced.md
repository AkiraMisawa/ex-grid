# A 3D surface takes a grid of values, and is turned, zoomed and sliced

*Decided with the user, 2026-10-19 (M2 grilling Q5 and Q6). The 3D entry is ADR-0255's twelfth.* *(The fourteenth of ADR-0021's list since
ADR-0240's two joined it, 2026-10-19.)*

## The data is a grid

**A surface is x values × y values × a table of z** — strikes × tenors × volatilities. A missing value
is a hole in the surface: never 0 and never filled from its neighbours. Scattered points are refused
by name. Joining them into triangles is interpolation, and ExChart chooses none (ADR-0257): a
triangulation's choice changes the surface's shape, and a plausible surface the analytics never
produced is the quiet error spine rule 1 forbids.

*Widened 2026-10-19, with the user, after a survey of 3D charts elsewhere ("in some cases interpolation
seems fine"; "A is fine, but could the chart offer it as a helper?").* The rule above still holds — the
chart never interpolates on its own — but a surface may now arrive **interpolated by the Consumer**,
in two ways, and says so:

- **As a function z(x, y)** over a rectangle, with its method's name. ExChart samples it on a grid and
  draws a hole wherever it returns no value. This is the 3D form of ADR-0257's `valueAt(t)`.
- **As a grid whose interpolated values are marked**, with the method's name.

Either way the method's name is written in the Notes and the tooltip says, at each grid point, whether
the value is quoted or interpolated by that method; interpolated points carry no dot, so the dots on the
surface are the data's. The grid stays the one shape ExChart draws: a function, or a Consumer's triangle
mesh evaluated through ADR-0257's helpers, becomes a grid before it is drawn, so a later input shape adds
a way in and changes nothing drawn.

*Amended 2026-10-20, with ADR-0311.* The last sentence held until a mesh came in. A Consumer's
triangle mesh is now drawn as its own triangles, not resampled onto a grid, because a grid through a
P1 mesh is not the Consumer's function. A function, and the helpers, still become a grid. The surface
also gained its colour bar and colours that may diverge from a centre (ADR-0310), and axes placed by
value or by logarithm (ADR-0312).

## What the user does

- **Turn** it by dragging, or with the arrow keys.
- **Zoom and pan** it (the user chose this over leaving them out, 2026-10-19).
- **Read** a value: the tooltip names the grid point nearest the pointer — x, y and z, each through its
  format. It never reads a value between grid points.
- **Slice** it: a click on a tenor or a strike draws that slice — a smile, a term structure — as a
  2D chart beside the surface, where values are read exactly. Depth makes a 3D value hard to read; the
  slice is where it is read.

Angles, zoom and centre are raised and taken back as ADR-0272 says, so a redraw keeps the view.
