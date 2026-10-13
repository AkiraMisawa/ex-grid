Product: ExGrid
Kind: Behaviour
Docs: exgrid/appearance#visual-tokens
ADR: 0008, 0029

In a Selection of several ranges, made with Ctrl+click, the Focus cell is marked by a thin border of one Device Pixel inside the cell, where it had the 2px Focus outline; no range carries an outline and no fill handle is shown, as before. The border's colour is the new Visual Token `--ex-focus-among-ranges-outline`, which is the Focus outline's colour (`--ex-focus-outline`) unless set, so a Theme that colours the Focus, and ExGrid.MudBlazor's primary, colour this border too. One cell and a single range look as they did.
