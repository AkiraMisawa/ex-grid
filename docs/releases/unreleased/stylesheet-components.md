Product: ExGrid
Kind: Breaking
Docs: getting-started#link-the-stylesheets
ADR: 0123

The stylesheets are placed as components, not linked by path: replace your `<link>` to `_content/ExGrid/ex-grid.min.css` with `<ExGridStylesheet />` in `App.razor`'s `<head>` in a Blazor Web App, or add it as a root component at `head::after` in a standalone application, and do the same with `<MudExGridStylesheet />` for ExGrid.MudBlazor. Each component, and the grid's own script import, asks for the file the package was built with, so a normal reload after an upgrade brings the new files, on GitHub Pages too; `<ImportMap />` is no longer needed for the grid's script.
