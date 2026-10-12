Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.OverflowRules` is no longer public: it is how the grid decides between `####` and an ellipsis for a value that does not fit (ADR-0016). Nothing replaces it; `CellTextMetrics.EstimatePx` still gives the width the grid charges a value.
