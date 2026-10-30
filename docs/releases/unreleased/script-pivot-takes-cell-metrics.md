Product: ExPivot
Kind: Added
ADR: 0016, 0030

Blazor's ExPivot over the Script Core (`ExPivot.Components.ExPivot`) takes `CellMetrics` and a Wrapper's cascaded `GridPresentationDefaults`, as the Razor component does: inside `MudExGridPaper` the report's grids and its label columns are sized by the paper's Roboto widths and Density. Until now they charged the default font's widths under the paper's Roboto. Default `CellTextMetrics` are refused by name.
