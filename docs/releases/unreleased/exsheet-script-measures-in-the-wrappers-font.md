Product: ExSheet
Kind: Fixed
ADR: 0030

Blazor's ExSheet over the Script Core (`ExSheet.Script.ExSheet`) inside a `MudExGridPaper` measured its columns and judged `####` by the system font's widths while its cells were painted in the paper's Roboto, so a number that fit could show `####` and one that did not could be cut. It now takes the paper's `GridPresentationDefaults`, as the Razor component does, and `@ex-grid/sheet` takes a Wrapper's `presentationDefaults`, which its columns are measured in and its grid is handed.
