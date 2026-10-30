Product: ExGrid
Kind: Added
ADR: 0380

A column's verdict may be answered later: `GridColumn.ValidateAsync` declares one, with a `CancellationToken` that Escape cancels, and while it, a column's `Validate`, a `Refuse` from `OnEdit` or `OnPaste`, or a `CellState` or `CellMessageOf` answer is out, the commit is pending and the input behind it waits, so on Blazor Server a commit costs one round trip (ADR-0380 section 5). `GridEditIntent.CancellationToken` and `GridPasteIntent.CancellationToken` tell a handler the user took the commit back, and `OnRefused` hears it with the new `CommitRefusalReason.Cancelled`. In the Script Core, `cellMessage` may answer with a promise, and the message opens once it has.
