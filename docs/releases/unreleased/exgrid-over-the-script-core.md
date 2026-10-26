Product: ExGrid
Kind: Added
ADR: 0350

`ExGrid.Script.ExGrid<TRow>` is Blazor's ExGrid drawn by the TypeScript Script Core, the first step of moving Blazor onto it (ADR-0350 section 7): the keys, the Selection, the clipboard and the editor run in the browser on both hosting models, while the rows, their order and what an edit does to them stay in .NET behind the same `GridColumn`s and `IGridSource`. Template Columns, `Validate`, Tone, Row Marks and refusing an edit or a paste are not carried yet and are refused by name, and Find and the Selection Summary are not offered; `ExGrid.Components.ExGrid` still does all of them. Building ExGrid from source now needs Node 26.12.1, or a release of 22 or 24 with the same fixes (22.23.4, 24.22.1), which bundles the core.
