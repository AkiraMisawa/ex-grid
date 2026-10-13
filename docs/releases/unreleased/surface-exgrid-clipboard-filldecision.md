Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.FillDecision` is no longer public: it is the grid's verdict on a fill key before it asks anyone. A Consumer receives an approved fill as a `GridPasteIntent`, and hears a refusal as a `PasteRefusalReason` through `OnPasteRefused`.
