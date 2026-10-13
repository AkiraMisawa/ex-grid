Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Selection.FillExtension` is no longer public: it is the grid's own reading of where a fill-handle drag reaches. A Consumer receives the same as a `GridFillIntent`'s `Target` and `Direction`.
