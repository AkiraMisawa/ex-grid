Product: ExChart
Kind: Added
ADR: 0321

A Chart Spec's observation may name the columns its tooltip says, `"tooltip": ["exercised"]` (in C#, `ChartSeries.Details` of `PointDetail`); the tooltip says no other column, and the table and the CSV carry them too. With the pointer near an edge and no node in reach, the tooltip says the edge's two keys (`ChartScene.EdgeMarks`, `ChartReadings.OfEdge`).
