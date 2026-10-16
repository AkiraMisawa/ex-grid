Product: ExGrid
Kind: Behaviour
ADR: 0051

Where `PointAt` says a Reference can go, the Primary Modifier's arrows now point at the edge `DataEdge` answers, as they move a Selection there with no edit open, and with Shift write the range from the edited cell, or from the outline's fixed end, to it: `=` then Ctrl+↓ on an empty column of a Sheet writes `=D1048576`, and Ctrl+Shift+↓ `=D10:D1048576`, as run 18a read in Excel. They were claimed and did nothing there. The key listener claims them in Point as it does in Overwrite. And the selection a `CycleReference` function answers is now placed as given wherever the caret stood before the text's end, and collapsed to a caret at its end where the caret stood at the text's end, however it got there: a caret moved back to the end leaves a caret, and a caret typed at inside the text selects (run 18a). It was placed by whether the user had moved the caret in that text.
