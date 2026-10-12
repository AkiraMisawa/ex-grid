Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#borders
ADR: 0008, 0071

In a Selection of several ranges, made with Ctrl+click, the active cell takes a thin border of one Device Pixel inside it, where it had the 2-pixel outline with its white line: `#217346`, the outline's green, as the readings of 2026-10-12 measured Excel's active cell among several areas, and the palette's primary under ExSheet.MudBlazor. `--ex-focus-among-ranges-outline` colours it. No range carries an outline and there is no fill handle among several ranges, as before.
