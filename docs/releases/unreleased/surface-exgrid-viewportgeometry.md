Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.ViewportGeometry` is no longer public: it is the grid's own vertical slicing and scroll compression (ADR-0013, ADR-0053). `GridMetrics.LargestRowHeightFor` answers the tallest row a result can carry under the browser's scroll ceiling, and ExSheet now asks it there.
