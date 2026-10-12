Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Keys.GridKeyClaims` is no longer public: it is how the grid decides which of its own keys to claim from the browser. A Consumer declares keys of its own with `DeclaredKeys`, whose documentation states the canonical form they are written in.
