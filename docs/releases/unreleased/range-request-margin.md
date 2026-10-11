Product: ExGrid
Kind: Added
Docs: exgrid/data#asking-before-the-viewport-runs-out
ADR: 0183

`RangeRequestMargin` lets the grid raise its Range Request while rows are still on screen, as soon as the rows on screen plus that many either side are not all in the Window. It is 0 by default, which changes nothing; a source that reads ahead sets it.
