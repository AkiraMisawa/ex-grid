Product: ExSheet
Kind: Fixed
Docs: exsheet#a-first-sheet
ADR: 0050

Whole columns selected from their Headings show the fill handle at the top-right, beside the Heading, and whole rows at the bottom-left, and every cell shows none, as Excel did (observed by the Windows run of 2026-10-11, h3, and the readings of 2026-10-12, R5 (1) and (2)); dragged, they fill every row (every column), a lone number going on by one while one cell's handle still copies it, as Excel did (observed, R5 (3) and (4)). A fill from whole columns or whole rows, by the handle or by Ctrl+R and Ctrl+D, records the source's column (row) format on the target columns (rows) and writes only the cells that hold something, so its cost is those cells, not a million: column C made bold and filled into D:E records bold on D and E, where each of their two million cells was written bold.
