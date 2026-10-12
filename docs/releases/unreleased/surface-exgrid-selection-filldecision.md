Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Selection.FillDecision` is no longer public: it is the grid's verdict on a fill-handle drag before it asks anyone. A Consumer receives an approved fill as a `GridFillIntent` through `OnFill`, and hears a refusal as a `PasteRefusalReason` through `OnPasteRefused`.
