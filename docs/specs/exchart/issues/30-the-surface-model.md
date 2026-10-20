# 30: The surface model

Status: done

**Why:** [ADR-0274](../../../adr/0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md); CH-7.

- [x] `SurfaceDefinition`: x values × y values × a table of z; a missing value is a hole, never 0
- [x] Scattered points refused by name; a row of the wrong length, a value named twice and a surface of holes refused
- [x] `SurfaceMesh`: the vertical scale and its ticks, each face's colour band, the holes; the accessible summary
- [x] A slice as a 2D chart along the other axis, named in the data's terms

Roadmap: internal
