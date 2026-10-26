Product: ExGrid
Kind: Added
ADR: 0050, 0370

The Script Core's grid places the Selection and the Focus for a Consumer, as `ExGrid.PlaceSelectionAsync` does: `GridHandle.placeSelection(range, focus, rowSequenceVersion)` commits an open edit first and scrolls the Focus into view, and a request written under another Row Sequence Version than the source's, or whose commit brought in another source or moved the columns, places nothing and is refused through `onRefused` (kind `placement`, reason `orderMoved`, `sourceChanged` or `columnsMoved`). A ref on `@ex-grid/react`'s `ExGrid` reaches it (`ExGridApi`). It is published with 0.3.0.
