Product: ExGrid
Kind: Added
Docs: exgrid/selection#rectangles--a-focus-and-an-extent
ADR: 0008, 0029

The Heading Mark shows every selected column and row in the header and Row Headings, where present: a shade, coloured letters, and a line two device pixels thick on the edge toward the cells. A whole column or row takes a distinct shade, including when several ranges together cover it. The Headings corner's triangle takes the mark's colour when every cell is selected. Data rows do not repaint for these marks.

`--ex-heading-mark` colours the line, letters and selected triangle, using the Focus outline's colour unless set. `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the two shades. A Theme's tokens win; the bare grid uses system colours and ExGrid.MudBlazor uses its palette. The Headings follow the frame's colour scheme. Forced colours keep the line and marked letters in `Highlight` and drop the shades.

Where a header or Row Heading covers a side of the Focus or a single range, the mark supplies its two device pixels without a second outline beside it. At a pinned block's edge, or a grid edge without a Heading, the outline stays inside the cell.
