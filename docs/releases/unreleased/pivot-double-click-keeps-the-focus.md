Product: ExPivot
Kind: Fixed
ADR: 0240

A double click on an outer Item's label collapses or expands it and keeps the Focus on it, on Blazor Server as on WebAssembly. Over a circuit's round trip the report could be left with no Focus, about one time in three; the Focus is now placed once the toggled report has reached the grid (ADR-0240's note on ticket 05).
