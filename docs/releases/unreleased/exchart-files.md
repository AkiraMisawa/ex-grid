Product: ExChart
Kind: Added
Docs: exchart/export#csv--svg-and-png
ADR: 0282

A chart saves its CSV, SVG and PNG on one click, from its toolbar, its context menu or `DownloadAsync`. The SVG is the drawing on screen with its stylesheet inside it, the PNG is painted from it at the Device Pixel, and both take the theme's colours as the screen shows them.
