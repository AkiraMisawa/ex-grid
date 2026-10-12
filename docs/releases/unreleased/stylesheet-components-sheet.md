Product: ExSheet
Kind: Breaking
Docs: getting-started#link-the-stylesheets
ADR: 0123

ExSheet's stylesheet is placed as a component: replace your `<link>` to `_content/ExSheet/ex-sheet.min.css` with `<ExSheetStylesheet />`, and the ExSheet.MudBlazor one with `<MudExSheetStylesheet />`, in `App.razor`'s `<head>` or as root components at `head::after`, beside ExGrid's. Each asks for the file the package was built with, so a normal reload after an upgrade brings the new one.
