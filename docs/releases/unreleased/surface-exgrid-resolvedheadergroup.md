Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.ResolvedHeaderGroup` is no longer public: it is a header group as the grid lays it out over the columns shown. A Consumer declares groups with `HeaderGroups`, and nothing replaces the type.
