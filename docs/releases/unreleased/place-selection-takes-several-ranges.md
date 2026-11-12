Product: ExGrid
Kind: Added
Docs: exgrid/selection#placing-a-selection
ADR: 0050

A Consumer can place a Selection of several ranges, for rows that meet a condition and do not stand together.
`PlaceSelectionAsync` has an overload that takes a list of ranges, and `placeSelection` in `@ex-grid/core` and on ExGrid
for React's ref takes a range or an array of them. The ranges are placed as handed: in their order, ranges that overlap
neither joined nor refused, with the Focus on the cell named, so the Selection read back is the one placed. A request
under another Row Sequence Version is refused as the one-range form is, and no range, a range outside the grid or a Focus
in none of the ranges throws. The Selection Summary's reference answer (`GridSource.From`, `GridSummary.Bands`,
`summaryBands`) no longer slows with the square of the number of ranges: ten thousand one-row ranges took 2.4 seconds to
their first paint, and take 81 ms now.
