Product: ExChart
Kind: Fixed
Docs: exchart/panels#the-legend
ADR: 0271

A legend entry hidden or shown with Enter or Space keeps the keyboard, so the next Tab goes on to the next entry: the
legend is drawn again on a toggle, and the entry that held the keyboard went with the old drawing, which sent the
keyboard back to the start of the page. A legend drawn again by `hiddenSeries` (`HiddenSeries` in C#) or by new data
keeps it on the same series' entry too.
