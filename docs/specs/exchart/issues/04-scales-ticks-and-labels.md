# 04: Scales, ticks and labels for dates, numbers and categories

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0250](../../../adr/0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md) (formats shared with the cells), the proof of concept's `XAxisStyle`.

- [ ] Ticks: calendar boundaries, every date in the data, tenors from the first date, declared positions, categories
- [ ] Labels in the formats the cells use; angle 0°, 45°, 90°; a label that would touch its neighbour is left out, every tick still drawn
- [ ] Text width from the grid's glyph estimate, never measured (ADR-0021, ADR-0016) — or the gap named if that estimate cannot be shared without a dependency
- [ ] Vertical axes: nice ticks, from zero where the Series Kind or the series says so

**Blocked by:** 02.
