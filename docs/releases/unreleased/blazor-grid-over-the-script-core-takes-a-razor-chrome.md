Product: ExGrid
Kind: Added
ADR: 0410

Blazor's ExGrid over the Script Core (`ExGrid.Script.ExGrid`) takes a `Chrome`, and the parts a Chrome draws in Razor stand inside the grid the browser draws: the component renders each beside the grid and the grid's script moves it into place and wires its controls there. `MudGridChrome` draws the column menu, the filter panel, the Context Menu and the loading bar this way; over the Script Core its filter panel's operators are native selects and a condition's value is typed as text for every type, so no choice reaches the grid after the Enter that applies it. A Chrome of your own names its parts in the new `IGridChrome.ScriptSlots` and answers each through the overload that takes a view (`ColumnMenu(GridMenuView)`, `ContextMenu(GridMenuView)`, `FilterPanel(GridFilterView)`, `LoadingIndicator(GridLoadingView)`), marking its controls with `ScriptSlotAttributes`; the members have defaults, so an existing Chrome compiles unchanged.
