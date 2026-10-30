Product: ExChart
Kind: Breaking
ADR: 0370

`GridChartBridge<TRow>.Grid`, which took the grid drawn in .NET, is removed with that grid: set `ScriptGrid` to the `ExGrid.Components.ExGrid<TRow>` whose selections the bridge charts.
