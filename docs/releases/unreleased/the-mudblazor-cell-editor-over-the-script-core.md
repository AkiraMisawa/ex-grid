Product: ExGrid
Kind: Added
ADR: 0410

`MudGridChrome` draws the Cell Editor of `ExGrid.Components.ExGrid`, a bare input with Material's underline, through the new `IGridChrome.CellEditor(GridCellEditorView)` and `ScriptSlots.CellEditor`, its field marked with `ScriptSlotAttributes.EditorField` and read by the grid in the browser, so text typed at once on Blazor Server is what the Enter commits.
