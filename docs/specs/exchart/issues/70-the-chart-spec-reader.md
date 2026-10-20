# 70: The Chart Spec reader

Status: done
Roadmap: internal

**Why:** [ADR-0320](../../../adr/0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md).

- [x] A column seam in ExChart.Engine, `IChartData`, and its in-memory reference implementation `ChartData`
- [x] `ChartSpec.Read(json, tables)` turns a Chart Spec into a `ChartDefinition`: the axis, each series' Data Handle, columns and `kind`, panels, axis sides, Notes; Price Ranges from their four columns
- [x] A required `version`; refusals name the path and the reason (unknown version, unknown handle or column, missing `kind`, a value of the wrong type, numbers in the spec)
- [x] The definition's own refusals come back with the series' path
