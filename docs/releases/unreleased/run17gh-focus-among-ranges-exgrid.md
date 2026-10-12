Product: ExGrid
Kind: Behaviour
Docs: exgrid/appearance#visual-tokens
ADR: 0008, 0029

In a Selection of several ranges, made with Ctrl+click, the Focus cell is marked by a thin border of one Device Pixel inside the cell, in the new Visual Token `--ex-focus-among-ranges-outline` (`CanvasText` unless set), where it had the 2px Focus outline; no range carries an outline and no fill handle is shown, as before. A Theme that colours `--ex-focus-outline` and wants this border in that colour sets the new token too; one cell and a single range look as they did.
