# 31: The surface component and the 3D entry

Status: done

**Why:** [ADR-0255](../../../adr/0255-two-javascript-entries-the-charts-pointer-and-the-3d-surface.md) (the 3D entry), [ADR-0272](../../../adr/0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md), [ADR-0274](../../../adr/0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md).

- [x] `ExSurface`: turned by dragging or the arrow keys, zoomed with the wheel or + and -, panned with Shift; Reset view
- [x] The tooltip at the grid point nearest the pointer; a click on a tenor or a strike draws its slice beside the surface
- [x] View and slice raised and taken back; a report against an earlier mesh is dropped; a key pressed while a report crosses is kept
- [x] `attachSurface` in `ex-chart.js`: projects, orders, fits and draws; formats no number and measures nothing; colours are Visual Tokens

**Blocked by:** 30.

Roadmap: internal
