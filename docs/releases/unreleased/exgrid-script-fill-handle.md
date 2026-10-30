Product: ExGrid
Kind: Behaviour
ADR: 0050

`ExGrid.Components.ExGrid<TRow>`'s fill is pending in the browser until `OnFill`'s handler completes, as a paste is (ADR-0380): a target over a column that is not Editable is refused first through `OnRefused`, a handler that calls `GridFillIntent.Refuse` leaves the Selection on the source, Escape cancels a fill whose handler has not completed, through the new `GridFillIntent.CancellationToken`, and otherwise the Selection becomes the source and the target together.
