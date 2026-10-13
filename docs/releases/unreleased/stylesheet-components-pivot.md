Product: ExPivot
Kind: Breaking
Docs: expivot#installation
ADR: 0123

ExPivot's stylesheet is placed as a component: replace your `<link>` to `_content/ExPivot/ex-pivot.min.css` with `<ExPivotStylesheet />`, and the ExPivot.MudBlazor one with `<MudExPivotStylesheet />`, in `App.razor`'s `<head>` or as root components at `head::after`, beside ExGrid's. Each asks for the file the package was built with, so a normal reload after an upgrade brings the new one.
