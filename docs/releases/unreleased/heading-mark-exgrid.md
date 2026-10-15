Product: ExGrid
Kind: Added
Docs: exgrid/selection#rectangles--a-focus-and-an-extent
ADR: 0008, 0029

The Heading Mark shades and colours every selected column and row in the header and Row Headings, where present, and draws a two-device-pixel line toward the cells; a whole column or row takes a distinct shade, the corner triangle takes the mark's colour when every cell is selected, and data row components do not render again. `--ex-heading-mark`, `--ex-heading-mark-background` and `--ex-heading-mark-whole-background` set the line, letters, triangle and shades, with the Focus outline's colour as the mark's default; Themes take precedence, the bare grid uses system colours, ExGrid.MudBlazor uses its palette, the Headings follow the frame's colour scheme, and forced colours keep the line, letters and Focus border in Highlight while dropping shades. Where a header or Row Heading covers a Focus or single-range side, the mark supplies its two device pixels without a second outline; at a pinned block or unheaded grid edge the outline stays inside the cell, with a two-device-pixel default at every display scale.
