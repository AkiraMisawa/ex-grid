Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.HeaderGroupLayout` is no longer public: it is the grid's own layout of the header groups over the columns shown. A Consumer declares groups with `HeaderGroups`, and nothing replaces the type.
