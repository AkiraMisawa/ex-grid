Product: ExChart
Kind: Added
Docs: exchart/surface#logarithmic-axes
ADR: 0312

A surface's axes may place their values in proportion or by their logarithm (`SurfaceAxis.Scale`), and its values may be on a logarithmic scale (`SurfaceDefinition.ZScale`), which stays proportional with a Note when a value is at or below zero. An axis left as it was keeps its values evenly spaced, as before.
