Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.SortCycle` is no longer public: it is the order a header click cycles a column's sort through (ADR-0012). Nothing replaces it; a Consumer hears the sort a click asks for through `OnSortChanged`.
