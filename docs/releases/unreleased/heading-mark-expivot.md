Product: ExPivot
Kind: Added
Docs: expivot/layout#report-forms--subtotals-and-grand-totals
ADR: 0008, 0059

The report's header shades and colours the letters of each selected column, with a two-device-pixel line along its bottom edge including the pinned label column; a whole column takes a distinct shade, including when several ranges together cover it. `--ex-heading-mark`, `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the line, letters and shades, with the Focus outline's colour as the mark's default; Themes take precedence, ExPivot.MudBlazor uses its palette, and the header follows the frame's colour scheme. Where the header covers a Focus or single-range side, the mark supplies its two device pixels without a second outline; at the pinned block the outline stays inside the cell, with a two-device-pixel default at every display scale and forced colours preserving the Focus border and Heading Mark in Highlight.
