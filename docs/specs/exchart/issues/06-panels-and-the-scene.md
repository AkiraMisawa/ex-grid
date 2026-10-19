# 06: Panels and the scene of shapes

Status: done

**Why:** [ADR-0252](../../../adr/0252-a-chart-renders-as-svg-elements-in-components-that-skip-their-render.md): the components render immutable pieces of a scene, compared to skip their render.

- [x] A panel per declared group by default, left and right axes with zeros at the same height unless the Consumer declares independent scales, which adds a note (ADR-0251)
- [ ] A series with negatives beside positives (a Diff) in a panel of its own by default (ADR-0251) — moved to M3: the grid bridge is what assigns panels when it builds a Chart Definition from a selection; the Engine draws the panels a definition names
- [x] The scene as immutable records (panel, axis, grid lines, one piece per series), equal when unchanged, with no colour, line pattern or marker in it — only the Appearance Keys' slots (ADR-0027, ADR-0256); Notes and Segments' methods placed under the chart (ADR-0257)
- [x] Layer-1 tests: a change to one series changes only that series' piece

**Blocked by:** 03, 04, 05.

## Comments

- 2026-10-19: Zero alignment falls back to independent scales, with the same note, when a side's range cannot reach zero by stretching; the note says so rather than leaving the axes quietly unaligned.
