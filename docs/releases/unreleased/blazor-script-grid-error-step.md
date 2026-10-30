Product: ExGrid
Kind: Added
ADR: 0381

`ExGrid.Components.ExGrid<TRow>` takes `OnFindError`, the step to the next and the previous error cell, so F8 and Shift+F8 work on Blazor's Script Core grid: the Consumer answers in .NET, typically with `GridRuleset.FindError(request, rowsInOrder)` over the rows its Source holds in the grid's order (ADR-0381 section 3). A step asked under an order the Source has since left is not handed to the Consumer, and the grid says the rows moved. `OnFindError` takes a `Source`, and is refused beside `Rows`, whose order the Consumer cannot read.
