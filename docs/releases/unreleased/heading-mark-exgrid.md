Product: ExGrid
Kind: Added
Docs: exgrid/selection#rectangles--a-focus-and-an-extent
ADR: 0008, 0029

The header shows where the selection is, as Excel's column letters and row numbers do: every column that holds part of the selection has a line two device pixels thick along the bottom of its header, and where the grid has Row Headings, every such row has one down its Row Heading's right edge — for every selection, one cell included. It is drawn over the header, so no row is painted again for it. Its colour is the new Visual Token `--ex-heading-mark`, the Focus outline's colour (`--ex-focus-outline`) unless set, so a Theme that colours the Focus, and ExGrid.MudBlazor's primary, colour it too; under forced colours it is drawn in `Highlight`.
