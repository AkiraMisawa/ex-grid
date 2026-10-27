Product: ExGrid
Kind: Added
ADR: 0050

`ExGrid.Script.ExGrid<TRow>` takes `ShowFillHandle` and `OnFill`, as `ExGrid.Components.ExGrid` does: the Script Core paints the handle and raises one `GridFillIntent` per drag, and a target over a column that is not Editable is refused first through `OnRefused`. The fill is pending in the browser until the handler completes, as a paste is (ADR-0380): a handler that calls `GridFillIntent.Refuse` leaves the Selection on the source, Escape cancels a fill whose handler has not completed, through the new `GridFillIntent.CancellationToken`, and otherwise the Selection becomes the source and the target together.
