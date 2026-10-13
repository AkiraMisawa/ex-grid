Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.CopyDecision` is no longer public: it is the grid's verdict on a copy before it asks anyone. A Consumer answering a copy receives the approved plan as `GridCopyRequest.Plan`, and hears a refusal as a `CopyRefusalReason` through `OnCopyRefused`.
