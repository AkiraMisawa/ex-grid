Product: ExChart
Kind: Fixed
Docs: exchart/export#read-aloud
ADR: 0275

An open "View as table" stays where the reader left it when the chart draws again: a series toggled from the legend,
new data, a narrowed range or a switched axis rewrites only the cells whose text changed, so the table keeps its
scroll position and the keyboard of a reader who tabbed into it, where it went back to its first row and dropped the
keyboard to the page's body. A 3D surface's table does the same.
