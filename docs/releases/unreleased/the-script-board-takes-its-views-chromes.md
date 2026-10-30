Product: ExBoard
Kind: Added
ADR: 0410, 0370

`ExBoard.Components.ExBoard` takes `Chrome`, `PivotChrome` and `ChartChrome`: the grid Chrome dresses the grids and the pivots' report grids, the pivot Chrome the pivots, its grid Chrome the report grids where no `Chrome` is given, and the chart Chrome the charts. Each part a Chrome names in its `ScriptSlots` is drawn by its Razor inside the view the browser draws, so `MudGridChrome`, `MudPivotChrome` and `MudChartChrome` dress it. `@ex-grid/board` adds `createBoardWith` and `familyMounts`, for a Framework Adapter that keeps each view's handle.
