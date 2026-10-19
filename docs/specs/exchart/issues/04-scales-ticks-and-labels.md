# 04: Scales, ticks and labels for dates, numbers and categories

Status: done

**Why:** [ADR-0250](../../../adr/0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md) (formats shared with the cells), the proof of concept's `XAxisStyle`.

- [x] Ticks: calendar boundaries, every date in the data, tenors from the first date, declared positions, categories
- [x] Labels in the formats the cells use; angle 0°, 45°, 90°; a label that would touch its neighbour is left out, every tick still drawn
- [x] Text width from the grid's glyph estimate, never measured (ADR-0021, ADR-0016) — or the gap named if that estimate cannot be shared without a dependency
- [x] Vertical axes: nice ticks, from zero where the Series Kind or the series says so

**Blocked by:** 02.

## Comments

- 2026-10-19: The Engine depends on nothing, so it cannot read ExGrid's glyph estimate. It takes text width through `ChartMetrics.TextWidth` (a character-count estimate when none is given); M2's `ExChart` passes the grid's estimate there. Amounts per year use 365.25 days a year (`ChartAxis.DaysPerYear`).
