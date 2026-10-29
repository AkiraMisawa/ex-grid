Product: ExGrid
Kind: Added
ADR: 0431

`ExGrid.Script.ExGrid` now takes `PointAt`, `ReferenceText`, `CycleReference` and `ReferencesIn`, each a `ScriptFunction` naming a function exported from a JavaScript module the application serves, which the browser calls as it decides each key in the editor, on Blazor Server as on WebAssembly. The modules load before the grid is drawn: a function first given later is refused by name, and a module that does not load is said in the grid's place. It also takes `OutlinedColumns` and `OnReferenceKeyColoursChanged`. `ReferenceColour.Reported` is new, for an adapter reading back a colour the Script Core handed out.
