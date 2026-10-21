Product: ExGrid
Kind: Fixed
ADR: 0240, 0008

A drag that leaves the grid before its press is answered stops the auto-scroll. On Blazor Server, or behind a busy page, the moves a drag makes before its press is answered are held and handed on after it; the pointer leaving the grid was not, so it reached the grid first, and the last move, in the edge band, started a scroll that ran on to the last row with the button long released. The leaving is now held behind those moves. ExSheet's and ExPivot's grids are fixed with it.
