# 37: A surface's axes by value and by logarithm

Status: done
Roadmap: ExChart — a surface's axes and values on a logarithmic scale

**Why:** [ADR-0312](../../../adr/0312-a-surfaces-axes-may-place-their-values-by-value-or-by-logarithm.md).

- [x] `SurfaceAxis.Scale`: evenly spaced (default), proportional or logarithmic, refused by name where it cannot place a value
- [x] `SurfaceDefinition.ZScale`: logarithmic, kept proportional with a Note at a value or a centre at or below zero
- [x] A grid's floor lines at its values, labelled where they do not crowd, a mesh's at round numbers; a sampled function sampled evenly in the logarithm; the slice keeps the scales

**Blocked by:** 30, 31.
