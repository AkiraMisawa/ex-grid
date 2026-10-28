Product: ExGrid
Kind: Added
ADR: 0016, 0030

Blazor's ExGrid over the Script Core (`ExGrid.Script.ExGrid`) takes `CellMetrics` and a Wrapper's cascaded `GridPresentationDefaults`, as the Razor component does: inside `MudExGridPaper` it takes the paper's Roboto widths, Density, hover band and Row Stripes, so `####` and an Auto width are judged by the font on screen. A value written on the grid beats the paper's. Until now it charged the default font's widths under the paper's Roboto. Default `CellTextMetrics` are refused by name.
