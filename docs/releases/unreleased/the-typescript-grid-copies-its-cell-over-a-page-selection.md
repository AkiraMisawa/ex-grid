Product: ExGrid
Kind: Fixed
ADR: 0005

On a display-only `@ex-grid/core` grid, Ctrl+C copied the text the user had selected elsewhere on the page instead of the grid's cell. A key on the grid's root now drops the page's text selection first, as the Blazor grid does (CP-10), so the copy is the grid's.
