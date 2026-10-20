# A Consumer's triangle mesh is drawn as its own triangles

*Decided while the user was away, 2026-10-20, under their standing instruction to pick a reasonable
default, record it in an ADR and carry on; the user asked for triangle-mesh input for the first version.
It amends [ADR-0274](0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md), which said a
mesh would become a grid before it is drawn; the user may revisit it.*

ADR-0274 drew a grid and nothing else. A Consumer with values on a triangle mesh (a finite element
solution, or quotes it triangulated itself) could already reach the surface: through ADR-0257's
`Interpolate.Triangles`, sampled onto a grid. That path stays, but it is not the mesh. Resampling a P1
function onto a grid loses the nodes, which are the values. The grid's cells cut across the triangles,
so a kink along an edge is rounded off. And each cell is drawn through its four sampled corners, which
is not the plane the Consumer's triangle defines. The picture is close, and it is not the Consumer's
function: spine rule 1 again.

## Decisions

- **`SurfaceDefinition.FromTriangles(nodes, triangles, …)`.** The nodes are positions with a value
  (or none). The triangles are the nodes' indices. ExChart draws each triangle flat, through its three
  corners, which is exactly the Consumer's piecewise-linear (P1) function, and chooses nothing. The
  triangulation is the Consumer's, and so is its meaning.
- **The nodes are the values.** A dot marks each node. The tooltip reads the nearest node and never
  a value between nodes, as it reads the nearest grid point (ADR-0274).
- **A node without a value is a hole.** No triangle at it is drawn, as a grid's cell with a hole at a
  corner is not. A domain with a hole in it has no triangles there, and is drawn with one.
- **A slice cuts the triangles exactly.** A click on a node slices along x or y through its position,
  whichever line the click lies nearer, as on a grid. The slice is the restriction of the P1 function
  to that line: straight on each triangle it crosses, broken where it crosses none. A slice may also
  be set at any position between the first node and the last (`SurfaceSlice.AtX(0.3)`). Its values
  between nodes are interpolated, linearly on the Consumer's triangles, so the slice is an
  Interpolated series that names `linear on the Consumer's triangles (P1)` (ADR-0257).
- **The axes place the nodes by position**, proportionally by default or logarithmically (ADR-0312).
  An evenly spaced axis means nothing for a mesh, and is refused by name.
- **Refused by name:** a triangle naming a node the mesh does not hold, a triangle without area
  (its corners on one line), two nodes at one place, a mesh with no triangle, and a mesh without a
  value. Not checked: overlapping triangles, or a mesh that is not conforming. Finding them is a mesh
  generator's work, quadratic done simply, and an overlapping mesh is drawn as given, so what is
  wrong is visible.
- **"View as table" lists the nodes**, x, y and value, because a mesh has no grid to tabulate.
- **The summary says** "a mesh of N nodes and M triangles" (ADR-0275).

## Rejected

- **Triangulating scattered points in ExChart (Delaunay).** It is still interpolation that ExChart
  would be choosing. A Delaunay triangulation of quotes is one choice among many, and it changes the
  surface's shape (ADR-0274). A Consumer who wants it builds its triangles and hands them in.
- **Higher-order elements (P2 and up).** Their values between nodes are not planes. Drawing them
  needs a refinement whose choice is ExChart's. A Consumer refines its own mesh into P1 triangles.
