Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#status-bar-figures
ADR: 0130

A grid that can summarise, as every grid bound to `GridSource.From` can, now shows the Selection Summary's line under its rows, and the line takes its height from the grid's. `ShowSelectionSummary="false"` takes it off; `OnSelectionSummaryChanged` is still told the figures (#65).
