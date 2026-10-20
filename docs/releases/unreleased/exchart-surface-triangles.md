Product: ExChart
Kind: Added
Docs: exchart/surface#on-your-own-triangles
ADR: 0311

`SurfaceDefinition.FromTriangles` draws a surface on the application's own triangle mesh, such as a finite element solution, as its triangles: never resampled onto a grid. The tooltip reads a node, and a click slices through it, cutting the triangles exactly and saying it reads between nodes linearly on them.
