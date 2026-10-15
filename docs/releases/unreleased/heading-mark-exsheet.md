Product: ExSheet
Kind: Added
Docs: exsheet/formatting#borders
ADR: 0008, 0071

The Heading Mark shades and colours the letters and numbers of every selected column and row, with a two-device-pixel line toward the cells; on a Sheet in the built-in light scheme its defaults are `#E1E1E1`, `#D3F0E0` for whole axes and `#217346` for letters, line and the corner triangle when every cell is selected, while headings between ranges stay unmarked. `--ex-heading-mark`, `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the line, letters, triangle and shades; Themes take precedence, ExSheet.MudBlazor uses its palette, and the Headings follow the frame's colour scheme. Where a Heading covers a Focus or single-range side, the mark supplies its two device pixels without a second outline; at the pinned block the outline stays inside the cell, with a two-device-pixel default at every display scale and forced colours preserving the Focus border and Heading Mark in Highlight.
