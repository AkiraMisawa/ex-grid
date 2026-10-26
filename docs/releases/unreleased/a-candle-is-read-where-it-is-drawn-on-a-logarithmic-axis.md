Product: ExChart
Kind: Fixed
ADR: 0270

On a logarithmic vertical axis, the tooltip placed a candle's values at a height worked out in proportion, not where the candle is drawn. So where more series stood at the pointer than the tooltip has room for, it could list the wrong ones and order them wrongly. It now reads a candle at the height it is painted.
