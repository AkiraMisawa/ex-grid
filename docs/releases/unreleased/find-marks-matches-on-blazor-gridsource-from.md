Product: ExGrid
Kind: Fixed
ADR: 0332

On Blazor's `ExGrid.Script.ExGrid`, a grid given `Rows`, or `GridSource.From` as its `Source`, marks every painted match of the Find panel's search again, and the current match apart, as the Razor grid did. Any other .NET source marks matches only through `FindMatch`.
