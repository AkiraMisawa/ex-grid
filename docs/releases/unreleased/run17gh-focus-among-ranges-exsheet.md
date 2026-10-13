Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#borders
ADR: 0008, 0071

In a Selection of several ranges, made with Ctrl+click, the active cell takes a thin border of one Device Pixel inside it, where it had the 2-pixel outline with its white line: `#217346`, the outline's green, as the readings of 2026-10-12 observed Excel's active cell among several areas (R4, at Excel's 100% and 150% zoom on a 150% display), and the palette's primary under ExSheet.MudBlazor. `--ex-focus-among-ranges-outline` colours it. No range carries an outline and there is no fill handle among several ranges, as before.
