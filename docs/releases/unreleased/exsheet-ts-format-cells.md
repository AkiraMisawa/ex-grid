Product: ExSheet
Kind: Added
ADR: 0071, 0370

The TypeScript Sheet (`@ex-grid/sheet`, and `@ex-grid/sheet-react`'s `ExSheet`) opens Format Cells on Ctrl+1 and from the Sheet Toolbar's Format Cells… item, in the grid's popover, as the Blazor Sheet does: the Number, Alignment, Font, Border and Fill tabs, opening on the Focus cell's format and on the tab last shown. OK sets the parts touched on every range of the Selection as one step to undo, widening the columns a number no longer fits; Cancel, Escape or a Selection moved sets nothing, and a Custom code ExSheet does not read is refused by name. Its labels are `SheetWords` (`formatCellsLabel`), in English and Japanese. The Context Menu's Format Cells… follows.
