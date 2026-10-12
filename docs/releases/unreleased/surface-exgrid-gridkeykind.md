Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Keys.GridKeyKind` is no longer public: it names the actions of the grid's own key table. A Consumer declares keys of its own with `DeclaredKeys` and hears them through `OnDeclaredKey`.
