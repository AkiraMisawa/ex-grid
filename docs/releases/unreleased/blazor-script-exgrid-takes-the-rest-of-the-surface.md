Product: ExGrid
Kind: Breaking
ADR: 0440

`RowHeadings`, `NameBoxLabel` and `DataEdge` on `ExGrid.Components.ExGrid`, and the new `FindMatch`, take a `ScriptFunction`, a function exported from a JavaScript module the application serves, which the browser calls as it draws and moves (ADR-0431), in place of a .NET delegate: a page passes `ScriptFunction.Of(module, export)` and serves the module.
