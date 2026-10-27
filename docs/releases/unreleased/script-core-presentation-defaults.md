Product: ExGrid
Kind: Added
ADR: 0028, 0030

ExGrid for React takes `cellMetrics` and `presentationDefaults`, as Blazor's ExGrid takes `CellMetrics` and a Wrapper's cascaded defaults: whoever sets `--ex-font-family` hands the grid that font's widths, so `####` is judged by the face on screen, and a design-system Wrapper's Density, hover band and Row Stripes stand where the grid is given none. `CellTextMetrics`, `GlyphWidthTable` and `GridPresentationDefaults` are exported. It is published with 0.3.0.
