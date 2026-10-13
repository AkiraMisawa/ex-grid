Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.ClipboardRules` is no longer public: it is the grid's own planning of a copy, a paste and a fill key. A Consumer answering a copy or a paste reads the plan the grid hands it (`GridCopyRequest.Plan`, `GridPasteIntent.Plan`), and `CopyCellCap` still defaults to the 1,000,000 cells `ClipboardRules.DefaultCellCap` named.
