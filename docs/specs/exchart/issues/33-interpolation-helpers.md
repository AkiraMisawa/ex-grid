# 33: Interpolation Helpers and the interpolated surface

Status: done

**Why:** [ADR-0257](../../../adr/0257-notes-and-interpolated-series-are-the-consumers-said-as-such.md) (Helpers, 2026-10-19), [ADR-0274](../../../adr/0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md) (widened 2026-10-19).

- [x] `ExChart.Engine.Interpolation`: linear, natural cubic spline, bilinear, linear on triangles (P1); each names its method and has no value outside its points
- [x] A surface handed in as a function z(x, y), or with interpolated values marked; the method in the Notes and the tooltip; no dot at an interpolated value

**Blocked by:** 30.

Roadmap: internal
