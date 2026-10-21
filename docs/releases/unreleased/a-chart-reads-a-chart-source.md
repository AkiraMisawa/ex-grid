Product: ExChart
Kind: Added
ADR: 0344

A table too large to send is read through a Chart Source: `ChartSpec.ReadAsync` asks each series' `IChartSource` for its column at a number of pixels, and draws the first, last, lowest and highest row of each pixel it answers with. `ChartSource.Thin` is the reference a server's source answers as. A series read this way says each pixel's whole count in its tooltip (`ChartSeries.Thinned`); its chart saves no CSV and shows no table, and what a thinned point cannot stand for is refused by path.
