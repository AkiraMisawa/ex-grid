Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.OverflowDecision` is no longer public: it is the grid's verdict on a value that does not fit its cell, `####` or an ellipsis (ADR-0016). Nothing replaces it; `CellTextMetrics.EstimatePx` still gives the width the grid charges a value.
