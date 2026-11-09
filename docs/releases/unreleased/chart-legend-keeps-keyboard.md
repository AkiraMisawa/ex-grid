Product: ExChart
Kind: Fixed
Docs: exchart/panels#the-legend
ADR: 0271

A legend entry hidden or shown with Enter or Space keeps the keyboard, so a second Enter reaches it and Tab goes on to
the next entry: the legend is drawn again on a toggle, and the entry that held the keyboard left with the old drawing.
A legend drawn again by `hiddenSeries` (`HiddenSeries` in C#) or by new data keeps the keyboard on the same series'
entry, or on the one drawn in its place when that series is gone.
