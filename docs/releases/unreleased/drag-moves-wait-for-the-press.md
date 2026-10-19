Product: ExGrid
Kind: Fixed
ADR: 0240
Docs: exgrid/selection

On Blazor Server, a drag made faster than a round trip — across the rows, across the headings, or on a grip or heading to size or move a column — moved nothing: its moves came before the grid had answered the press and were lost, so the selection stayed where the press left it. The moves are now held, with the release behind them, until the press has been answered, and the drag ends where the pointer did, as it does made slowly (ADR-0240). ExSheet and ExPivot, drawn by ExGrid, get the same.
