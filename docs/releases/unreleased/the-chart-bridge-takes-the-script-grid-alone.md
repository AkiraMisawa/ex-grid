Product: ExChart
Kind: Breaking
ADR: 0370

`GridChartBridge<TRow>.Grid`, which took the Razor `ExGrid.Components.ExGrid<TRow>`, is removed with that grid: set `ScriptGrid` to the `ExGrid.Script.ExGrid<TRow>` whose selections the bridge charts.
