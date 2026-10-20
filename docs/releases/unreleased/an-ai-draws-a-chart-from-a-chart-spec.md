Product: ExChart
Kind: Added
Docs: exchart/spec
ADR: 0320

An AI can draw a chart by writing a Chart Spec, JSON that names the tables a capability made and their columns and never holds a number. `ChartSpec.Tool` is the tool definition to hand a model; `ChartSpec.Answer` returns the chart said in words, or refuses with the path and the reason, and `ChartSpec.Read` gives the Chart Definition. A table is any `IChartData`: `ChartData.Of` for columns in hand, `SnapshotChartData` for a Snapshot.
