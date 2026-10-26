Product: ExGrid
Kind: Fixed
ADR: 0332, 0051

In `@ex-grid/core`, Replace in the find panel writes the replacement into the text an edit opens with, a Consumer's `editorText` where it declares one, as the Blazor grid does. Before, it wrote into the text the cell paints, so a cell whose `editorText` was a Formula was overwritten with a constant.
