Product: ExGrid
Kind: Added
ADR: 0410

Blazor's ExGrid over the Script Core (`ExGrid.Script.ExGrid`) takes a `Chrome`, and the parts a Chrome draws in Razor stand inside the grid the browser draws: the component renders each beside the grid and the grid's script moves it into place and wires its controls there. `MudGridChrome` draws the column menu, the Context Menu and the loading bar this way. A Chrome of your own names its parts in the new `IGridChrome.ScriptSlots` and answers each through the overload that takes a view (`ColumnMenu(GridMenuView)`, `ContextMenu(GridMenuView)`, `LoadingIndicator(GridLoadingView)`), marking a menu's controls with `ScriptSlotAttributes.MenuItem`; the members have defaults, so an existing Chrome compiles unchanged.
