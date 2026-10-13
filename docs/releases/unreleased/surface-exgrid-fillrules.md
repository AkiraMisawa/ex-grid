Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Selection.FillRules` is no longer public: it is the grid's own reading of a fill-handle drag (ADR-0050, item 5). A Consumer receives an approved fill as a `GridFillIntent` through `OnFill`.
