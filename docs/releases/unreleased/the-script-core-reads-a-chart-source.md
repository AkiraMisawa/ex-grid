Product: ExChart
Kind: Added
ADR: 0344

`@ex-grid/chart` reads a Chart Spec through Chart Sources, as `ChartSpec.ReadAsync` does: `readChartSpecThrough` asks each series' `ChartSource` for its column at a number of pixels and reads the first, last, lowest and highest row of each pixel it answers with, carrying each pixel's whole count to the tooltip. `thinChartData` is the reference a server's source answers as, and `chartSourceOver` thins a table held in the page. Both are held to the C# engine by the shared cases `tests/cases/chart/source.json`.
