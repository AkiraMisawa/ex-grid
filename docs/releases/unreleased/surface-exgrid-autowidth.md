Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.AutoWidth` is no longer public, and neither is `ColumnWidthSpec.ResolveWidthPx`, which took it: it tracks the widest value an Auto column has painted (ADR-0016). Nothing replaces it; a Consumer declares `ColumnWidth.Auto` and the grid resolves it.
