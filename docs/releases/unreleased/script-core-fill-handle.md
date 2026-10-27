Product: ExGrid
Kind: Added
ADR: 0050

The TypeScript core draws the fill handle: `showFillHandle: true` paints it at the Selection's corner (top-right for whole columns, bottom-left for whole rows), and a drag from it outlines the cells it reaches along one axis. The release raises one `GridFillIntent` to `onFill` with the source, the target, the direction and the Row Sequence Version; the grid writes nothing. A target over a column that is not Editable is refused whole before anything is raised, and after a fill the Consumer does not `refuse()`, the Selection is the source and the target together. `onFill` may answer later with a promise, as `onPaste` may (ADR-0380): until it settles the fill is pending, `refuse()` may still be called, and Escape cancels it, aborting `intent.signal`.
