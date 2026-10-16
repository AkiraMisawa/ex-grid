Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#borders
ADR: 0008, 0071

In a Selection of several ranges, the active cell's thin border now has a white line of one Device Pixel outside it on its top and left, between it and the gridline, and the cell's own ground on its right and bottom, so a filled active cell shows its Fill there and white on the other two sides, as run 18a observed Excel's with D4 filled yellow (at Excel's 100% and 150% zoom on a 150% display); before, the Fill showed on all four sides. `--ex-sheet-selection-ring` colours the line.
