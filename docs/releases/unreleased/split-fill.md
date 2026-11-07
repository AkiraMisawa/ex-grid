Product: ExGrid
Kind: Added
Docs: exgrid/appearance#font--fill-and-borders-per-cell
ADR: 0490

A cell's appearance may split its Fill diagonally between two colours: `fill` paints the half above and left of the
diagonal from the top-right corner to the bottom-left one, and the new `lowerRightFill` (`CellAppearance.LowerRightFill`
in C#) paints the half below and right of it. `lowerRightFill` without `fill` is refused by name.
