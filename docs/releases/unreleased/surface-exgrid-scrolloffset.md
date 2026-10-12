Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Components.ScrollOffset` is no longer public: it carries the scroll offsets the grid's script reports to the grid, and was public only for tests that stand in for that script. Nothing replaces it; a bUnit test of a page with a grid leaves the script's `getScrollOffset` to loose mode, which answers the grid's top-left.
