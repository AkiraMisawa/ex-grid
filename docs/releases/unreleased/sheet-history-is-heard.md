Product: ExSheet
Kind: Added
ADR: 0048

The Script ExSheet (`ExSheet.Script.ExSheet`) has `CanUndo` and `CanRedo`, as the Razor component does: the browser tells them as soon as a step moves the undo stack, before the document it made is heard and before `UndoAsync` or `RedoAsync` answers, and both are false once a document of the Consumer's has opened. The TypeScript Sheet adds `onHistoryChanged`, which hears whether there is a step to undo and one to redo each time either moves (a React `ExSheet` prop too). Every ExSheet, ExPivot and ExBoard Example on the Docs Site now draws the Script component.
