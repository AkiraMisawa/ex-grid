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

## How it turns

*Decided with the user, 2026-10-22, after the user tried the surface in the Docs app ("it moves the
opposite way to what I expect, and turning it is awkward") and then Plotly's 3D surface beside a copy
of ours ("the axes, and turning it round and round, are very intuitive").* The surface turns as
Plotly's default **turntable** does; three.js's OrbitControls and MATLAB's `rotate3d` agree with it:

- **It is seen from above, and never as a mirror.** The first drawing projected it as seen from below
  its floor: the walls "behind" it met at the floor's nearest corner on screen, the eye read the
  picture as a mirror image seen from above, and every turn then looked reversed. That was a defect,
  not a convention. The axes are right-handed with the vertical up; the walls and the floor behind the
  surface are the ones farthest from the viewer.
- **A turntable, not a trackball.** The yaw turns about the vertical and the pitch tilts it; there is
  no roll, so the value axis stays upright. An orbit that may roll was offered beside it and not chosen.
- **The surface is held where it is pressed.** A drag to the right brings its near side to the right;
  a drag down tips its top toward the viewer. An arrow key turns it as a drag the same way would.
- **A drag the box's height turns it a whole turn**, either way, so a turn takes the same gesture in a
  small box and a large one (OrbitControls; Plotly measured about the same).
- **The pitch runs from −90° to 90°**, so it may be seen from below, as in Plotly; seen from below, the
  box's top is behind it in place of its floor.
- **The right button, or Shift, pans; the wheel zooms by how far it moved**, so a trackpad's many
  small steps add up to a mouse's one step as far (one 100-pixel step is ×1.1).
- **One pointer at a time.** A second finger or button does not join a gesture; there is no pinch.
- **No inertia.** The view a drag leaves is where the pointer was released, whatever its speed
  (spine rule 6).

The survey and the measurements are kept with the project's research notes (3D interaction research,
2026-10-22).
