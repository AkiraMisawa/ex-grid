Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.PasteDecision` is no longer public: it is the grid's verdict on a paste before it asks anyone. A Consumer receives the approved plan as `GridPasteIntent.Plan`, and hears a refusal as a `PasteRefusalReason` through `OnPasteRefused`.
