Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.ViewportBox` is no longer public: it is the box the grid lays its rows out in once the browser's scrollbars are taken off (ADR-0013, ADR-0021). Nothing replaces it; the grid's size is still set with `ViewportHeight` and `ViewportWidth`.
