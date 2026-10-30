Product: ExGrid
Kind: Fixed
ADR: 0440

On Blazor's `ExGrid.Components.ExGrid`, with a Row Key, an open editor whose row a re-sort moves past the rows .NET sent to the browser now follows it there and its commit lands on it, as it does over a source held in the browser (LV-20). The TypeScript core's `GridSource` gains an optional `holdKey`, and its state an optional `beyond`, for a source that hands over a slice of the Window it holds.
