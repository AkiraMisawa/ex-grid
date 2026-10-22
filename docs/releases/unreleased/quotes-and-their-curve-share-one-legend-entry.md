Product: ExChart
Kind: Fixed
ADR: 0257

Quotes and the curve declared `Interpolated` that share their colour key, in one panel, now show one legend entry, as ADR-0257 says, instead of one each. Clicking it hides both; `OnSeriesVisibilityChanged` and `HiddenSeries` name the shared colour key.
