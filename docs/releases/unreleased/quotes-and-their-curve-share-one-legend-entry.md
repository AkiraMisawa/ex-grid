Product: ExChart
Kind: Fixed
ADR: 0257

Quotes and the curve declared `Interpolated` that share their colour key, in one panel, now show one legend entry, as ADR-0257 says, instead of one each. Clicking it hides both, and showing it again shows both; `OnSeriesVisibilityChanged` and `HiddenSeries` name the quotes' identity. Two quote series of one colour key keep an entry each. `ChartScene.LegendMembers` says which series an entry stands for.
