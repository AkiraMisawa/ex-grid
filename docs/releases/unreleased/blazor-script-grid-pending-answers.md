Product: ExGrid
Kind: Added
ADR: 0380

`ExGrid.Script.ExGrid<TRow>` now carries a column's `Validate`, a `Refuse` from `OnEdit` or `OnPaste`, and the `CellState` and `CellMessageOf` parameters, which it refused by name before: each answer crosses from .NET, and while it is out the commit is pending and the input behind it waits, so on Blazor Server a commit costs one round trip (ADR-0380 section 5). `GridColumn.ValidateAsync` declares a verdict answered later, with a `CancellationToken` that Escape cancels, and `GridEditIntent.CancellationToken` and `GridPasteIntent.CancellationToken` tell a handler the user took the commit back; the C# core's `ExGrid.Components.ExGrid` refuses a column that declares `ValidateAsync`, naming it. In the Script Core, `cellMessage` may answer with a promise, and the message opens once it has.
