Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.FillPlan` is no longer public: it is the grid's plan for the fill keys, Ctrl+D and Ctrl+R. A Consumer receives a fill key as a `GridPasteIntent` whose `FillSource` names the range it fills from.
