Product: ExChart
Kind: Added
ADR: 0253, 0258

`GridChartDeclarations.FromZeroOf` says, per column, whether a chart made from the grid's selection reads it from zero: `false` draws rates between 3.0% and 3.6% across the plot instead of as nearly flat lines from zero. A curve whose columns are declared differently is refused by name.
