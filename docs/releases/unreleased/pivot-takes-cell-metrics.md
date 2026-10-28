Product: ExPivot
Kind: Added
ADR: 0016, 0030

ExPivot for React and `@ex-grid/pivot` take `cellMetrics` and `presentationDefaults`, as ExGrid for React does: the report's grid and Show Details' grids charge their glyphs by them, and the label columns are sized by the same widths. `gridTextGeometry` and `gridHeaderRequiredPx` in `@ex-grid/core` take them too (`GridTextOptions`), so a label sized beside the grid is charged as the grid charges it.
