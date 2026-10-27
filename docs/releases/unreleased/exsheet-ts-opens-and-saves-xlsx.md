Product: ExSheet
Kind: Added
ADR: 0330, 0240

ExSheet for React opens and saves `.xlsx` through `@ex-grid/sheet-xlsx`: `open(read)` on the ref replaces the Workbook at its place in the Input Order, so what is typed while the file is read lands on the Workbook it opens, and Escape on the Sheet's cells, its toolbar or its tabs cancels the read; `save(write)` lends the Workbook as the inputs before it left it. The grid's ref gains the seams they are built on, `inOrder(handling)` and `cancellableInOrder(handling, cancelledTooLate)`. The toolbar and the tabs wait behind the opening as the cells do, as in Blazor. It is published with 0.3.0.
