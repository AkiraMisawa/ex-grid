Product: ExGrid
Kind: Fixed
ADR: 0008

A drag released where no rows are painted yet ends. On Blazor Server, while the edge band scrolls a drag, the rows under the pointer can still be unpainted; a release there was heard by nothing, and the drag, a Row Heading drag's Size Tip with it, went on until the next move. ExSheet's sheets are drawn by ExGrid and are fixed with it.
