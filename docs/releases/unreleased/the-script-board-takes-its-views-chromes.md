Product: ExBoard
Kind: Added
ADR: 0410, 0370

`ExBoard.Script.ExBoard` takes `Chrome` and `PivotChrome`, as the Razor `ExBoard` does: the grid Chrome dresses the grids and the pivots' report grids, the pivot Chrome the pivots, and its grid Chrome the report grids where no `Chrome` is given. Each part a Chrome names in its `ScriptSlots` is drawn by its Razor inside the view the browser draws, so `MudGridChrome` and `MudPivotChrome` dress it; the charts' Chrome is not carried yet. `@ex-grid/board` adds `createBoardWith` and `familyMounts`, for a Framework Adapter that keeps each view's handle.
