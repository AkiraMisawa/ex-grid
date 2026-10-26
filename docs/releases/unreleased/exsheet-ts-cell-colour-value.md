Product: ExSheet
Kind: Added
ADR: 0370

`@ex-grid/sheet-engine` exports `CellColour` as a value, not only as a type, so a Consumer makes a colour for a fill, a font or a Border with `CellColour.fromRgb(0x1F3864)`, as ExSheet.Engine's `CellColour.FromRgb` does.
