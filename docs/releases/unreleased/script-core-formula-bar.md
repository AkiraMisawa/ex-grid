Product: ExGrid
Kind: Added
ADR: 0051, 0370

`@ex-grid/core` draws the Formula Bar and its Name Box (`showFormulaBar`), as the Blazor component does: the band above the header reads the Focus cell's text and is the Cell Editor's second surface, one uncommitted text committed once from either place. The Name Box shows `nameBoxLabel`'s words for the Focus, and hands what is typed into it to `onNameBoxEntered`; the Consumer places the Selection with the handle's new `placeSelection`, which is refused once the Row Sequence Version it was taken against has moved, or while an edit stands. `formulaBarBorrowedText` shows another cell's text dimmed and read only over a cell with none of its own, as a spilled cell shows its Formula.
