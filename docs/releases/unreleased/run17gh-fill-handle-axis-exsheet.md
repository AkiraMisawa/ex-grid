Product: ExSheet
Kind: Fixed
Docs: exsheet#a-first-sheet
ADR: 0050

Whole columns selected from their Headings show the fill handle at the top-right, beside the Heading, and whole rows at the bottom-left, as Excel did in the Windows run of 2026-10-11 and the readings of 2026-10-12; every cell shows none, where Excel showed none in view. Dragged, they fill every row (every column) as Excel did in those readings, a lone number going on by one, though one cell's handle still copies it. A fill from whole columns or whole rows writes the source's column (row) format as the target columns' (rows') own, and then only the cells that hold something, so it costs those cells rather than a million: column C made bold and filled into D:E now records bold on columns D and E, where it wrote a Bold into some two million cells.
