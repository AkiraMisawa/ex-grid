Product: ExPivot
Kind: Added
Docs: expivot/layout#report-forms--subtotals-and-grand-totals
ADR: 0008, 0059

The report's header shades and colours the letters of each column the Selection holds, with a line two device pixels thick along its bottom edge, including the pinned label column. A whole column takes a distinct shade, including when several ranges together cover it.

`--ex-heading-mark` colours the line and letters in the Focus outline's colour unless set; `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the shades. A Theme's tokens win, ExPivot.MudBlazor uses its palette, and the header follows the frame's colour scheme.

Where the header covers a side of the Focus or a single range, the mark supplies its two device pixels without a second outline beside it. Against the pinned block, the outline stays inside the cell.
