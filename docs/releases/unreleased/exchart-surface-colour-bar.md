Product: ExChart
Kind: Added
Docs: exchart/surface#coloured-from-a-centre
ADR: 0310

A 3D surface draws its colour bar beside the plot, in its SVG and PNG too, so each colour reads back as a value. `SurfaceDefinition.Centre` makes the colours diverge from a value such as 0 for a P&L: red below, blue above, as deep on one side as on the other at the same distance, through the `--ex-chart-diverge-0` to `-8` Visual Tokens.
