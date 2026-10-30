Product: ExChart
Kind: Added
ADR: 0410

`ExChart.Components.ExChart` takes a `Chrome`. An `IChartChrome` names the slots it fills over the Script Core in `ScriptSlots` (`Toolbar`, `Menu`) and draws each from a view (`ChartToolbarView`, `ChartMenuView`), its controls marked with `ChartScriptSlotAttributes` so the chart's script runs a command, reads a typed range and switches the axis in the browser. `MudChartChrome` fills both, so MudBlazor's toolbar, Chart Filters and context menu now stand in a chart the Script Core draws; its downloads are a button group there, and Chart Filters' ends are typed in the axis' own writing (a date as yyyy-MM-dd) or chosen from the categories on a category axis.
