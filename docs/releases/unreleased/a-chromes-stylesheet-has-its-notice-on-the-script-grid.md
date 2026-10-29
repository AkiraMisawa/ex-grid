Product: ExGrid
Kind: Fixed
ADR: 0123

A Chrome's own stylesheet has its notice again on Blazor's `ExGrid.Script.ExGrid`: the stylesheets an `IGridChrome` brings, `ExGrid.MudBlazor`'s among them, cross to the browser, and the grid's root shows each one's notice when a stylesheet of another version is loaded (ERR-11). The TypeScript core's `GridChrome` gains `stylesheets`, a list of `{ name, stamp }` that its root renders a notice for beside its own.
