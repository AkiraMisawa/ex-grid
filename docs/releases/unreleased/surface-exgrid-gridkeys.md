Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Keys.GridKeys` is no longer public: it is the grid's own key table and canonical form (ADR-0010, ADR-0012). A Consumer declares keys of its own with `DeclaredKeys`, whose documentation now states that form, and hears them through `OnDeclaredKey`.
