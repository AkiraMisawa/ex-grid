Product: ExSheet
Kind: Added
Docs: exsheet/formatting#borders
ADR: 0008, 0071

The Heading Mark shades and colours the letters and numbers of every column and row the Selection holds, with a line two device pixels thick on their edge toward the cells. With the built-in Chrome in the light scheme, the shade is `#E1E1E1`, or `#D3F0E0` for a whole column or row; the letters and line are `#217346`. The Headings corner's triangle takes that green when every cell is selected. Whole means covered by the ranges' union; Headings between ranges stay unmarked.

`--ex-heading-mark` colours the line, letters and selected triangle; `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the shades. A Theme's tokens win, ExSheet.MudBlazor uses its palette, and the Headings' shades follow the frame's colour scheme in dark mode as well.

Where a Heading covers a side of the Focus or a single range, the mark supplies its two device pixels without a second outline beside it. Against the pinned block, the outline stays inside the cell.
