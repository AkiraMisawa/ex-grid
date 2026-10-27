Product: ExSheet
Kind: Added
ADR: 0048, 0071, 0180, 0331

ExSheet in TypeScript takes your commands, as the Blazor Sheet does: the handle, and the React component's ref, add `do(edit)` and `do(sheet, edit)`, `canUndo` and `canRedo`, `setCellFormat`, `openFormatCells` and `cellFormatAt`, each refused while a cell is edited, and the options add `onEditingChanged`, `onSelectionChanged` and `onSheetsChanged`. `RAND()` and `RANDBETWEEN` draw at once: the Sheet chooses a seed when it opens a document, where they showed `#GETTING_DATA` before, and `seed` fixes one. `document: undefined` opens an empty Sheet.
