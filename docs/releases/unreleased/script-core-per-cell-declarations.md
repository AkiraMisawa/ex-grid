Product: ExGrid
Kind: Added
ADR: 0370

`@ex-grid/core` takes what a Consumer that holds every cell declares per cell, as ExSheet does: a cell's own kind (`cellType`), alignment (`cellAlign`) and painted text fitted to its column (`paintedText`), its Font, Fill and Borders (`cellAppearance`, with `edgeBorder` choosing between two lines on one edge and `editorAppearance` for the Cell Editor), the text an edit opens on (`editorText`), a column header aligned apart from its cells (`headerAlign`), and a header click that selects the whole column instead of sorting (`headerClickSelects`). They are the Blazor component's parameters of the same names, painted to the same classes.
