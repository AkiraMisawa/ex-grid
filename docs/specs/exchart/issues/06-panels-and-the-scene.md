# 06: Panels and the scene of shapes

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0252](../../../adr/0252-a-chart-renders-as-svg-elements-in-components-that-skip-their-render.md): the components render immutable pieces of a scene, compared to skip their render.

- [ ] A panel per declared group by default, left and right axes with zeros at the same height unless the Consumer declares independent scales, which adds a note; a series with negatives beside positives (a Diff) in a panel of its own by default (ADR-0251)
- [ ] The scene as immutable records (panel, axis, grid lines, one piece per series), equal when unchanged, with no colour, line pattern or marker in it — only the Appearance Keys' slots (ADR-0027, ADR-0256); Notes and Segments' methods placed under the chart (ADR-0257)
- [ ] Layer-1 tests: a change to one series changes only that series' piece

**Blocked by:** 03, 04, 05.
