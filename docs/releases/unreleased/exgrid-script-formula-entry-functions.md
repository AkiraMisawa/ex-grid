Product: ExGrid
Kind: Breaking
ADR: 0431

`PointAt`, `ReferenceText`, `CycleReference` and `ReferencesIn` on `ExGrid.Components.ExGrid` take a `ScriptFunction`, naming a function exported from a JavaScript module the application serves, in place of a .NET delegate: the browser calls it as it decides each key in the editor, on Blazor Server as on WebAssembly (ADR-0431). The modules load before the grid is drawn: a function first given later is refused by name, and a module that does not load is said in the grid's place. `ReferenceColour.Reported` is new, for an adapter reading back a colour the Script Core handed out.
