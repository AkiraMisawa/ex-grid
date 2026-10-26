Product: ExGrid
Kind: Added
ADR: 0050

`ExGrid.Script.ExGrid<TRow>` takes `ShowFillHandle` and `OnFill`, as `ExGrid.Components.ExGrid` does: the Script Core paints the handle and raises one `GridFillIntent` per drag, and a target over a column that is not Editable is refused first through `OnRefused`. The browser has placed the Selection over the source and the target by the time .NET hears the fill, so a handler that calls `GridFillIntent.Refuse` fails the component with a `NotSupportedException`, as a refused edit or paste does there.
