Product: ExChart
Kind: Breaking
ADR: 0370

`ExChart.Components.ExChart`, `ExChart.Components.ExSurface` and `ExBoard.Components.ExBoard` are drawn by their Script Core in the browser in place of .NET, each with the same definition or Board. A .NET `Format` the browser cannot call is refused by name, and a `FormatCode` or a `ScriptFunction` replaces it. `IChartChrome`'s `Toolbar(ChartToolbarContext)` and `Menu(ChartMenuContext)`, with `ChartCommand`, `ChartToolbarContext`, `ChartFiltersContext` and `ChartMenuContext`, are removed, since a Chrome now fills only the Script Core's slots, through `ScriptSlots`, `Toolbar(ChartToolbarView)` and `Menu(ChartMenuView)`. In ExChart.MudBlazor, `MudChartToolbar` and `MudChartMenu` are removed, and so is `MudChartDock.ScriptCore`: the dock always draws its charts with the Script Core, beside `ExGrid.Components.ExGrid`. The pointer script `ex-chart.min.js` no longer ships.
