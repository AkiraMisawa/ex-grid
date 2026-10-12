Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.FilterOperators` is no longer public: it is the grid's table of the operators each column type offers (ADR-0023). A substituted Chrome receives them for its column as `FilterPanelContext.Allowed`.
