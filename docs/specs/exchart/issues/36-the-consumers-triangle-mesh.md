# 36: The Consumer's triangle mesh

Status: done
Roadmap: ExChart — a surface drawn on the Consumer's own triangles

**Why:** [ADR-0311](../../../adr/0311-a-consumers-triangle-mesh-is-drawn-as-its-own-triangles.md).

- [x] `SurfaceDefinition.FromTriangles`, each triangle drawn flat through its corners, never resampled
- [x] A node without a value is a hole; a malformed mesh is refused by name
- [x] The tooltip reads the nearest node; a click slices through its position, cutting the triangles exactly, as an Interpolated series naming P1
- [x] "View as table" lists the nodes

**Blocked by:** 30–33.
