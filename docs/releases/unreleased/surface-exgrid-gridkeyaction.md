Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Keys.GridKeyAction` is no longer public: it is what the grid's own key table resolves a key to. A Consumer declares keys of its own with `DeclaredKeys` and hears them through `OnDeclaredKey`.
