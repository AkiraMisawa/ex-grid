Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart` charts a grid's selection as `ExChart.GridChartBridge` does: `GridChartBridge` hands a grid's `contextCommands` "Chart", or "Series in columns" and "Series in rows", and "Add to chart" for each target, worded by `gridChartCommandLabel`, and `draw(chart)` reads a `GridChart` by Row Key from the Consumer's data, so a recalculated row is redrawn, a deleted row is a gap with a Note and a new row is never added (ADR-0258). A click on a point moves the grid's Focus through the bridge's `placeSelection`, which the Consumer hands the grid's own `placeSelection`. It is published with 0.3.0.
