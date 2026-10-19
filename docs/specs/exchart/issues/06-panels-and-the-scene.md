# 06: Panels and the scene of shapes

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0252](../../../adr/0252-a-chart-renders-as-svg-elements-in-components-that-skip-their-render.md): the components render immutable pieces of a scene, compared to skip their render.

- [ ] A panel per declared group by default, left and right axes, zeros aligned when both are drawn
- [ ] The scene as immutable records (panel, axis, grid lines, one piece per series), equal when unchanged, with no colour in it — only slots (ADR-0027)
- [ ] Layer-1 tests: a change to one series changes only that series' piece

**Blocked by:** 03, 04, 05.
