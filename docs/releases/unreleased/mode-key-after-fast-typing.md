Product: ExGrid
Kind: Fixed
ADR: 0230

On a Blazor Server circuit, an arrow, Home or End typed straight after text the grid had not yet answered is now judged in the mode that text leaves. Before, it could be judged in an older mode: `=SUM(` typed at once and then Home could move the caret as if a list of names were still open, where typed slowly Home points at A10. Characters are still typed at the browser's speed. ExSheet and ExPivot, drawn by ExGrid, behave the same.
