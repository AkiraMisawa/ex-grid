Product: ExSheet
Kind: Added
ADR: 0370, 0046, 0051

ExSheet runs without Blazor. `@ex-grid/sheet` is a Sheet drawn by `@ex-grid/core` as that grid's Consumer, over `@ex-grid/sheet-engine`: `createSheet(element, options)` edits Entries and Formulas, recalculates, pastes from Excel, fills with Ctrl+D and Ctrl+R, clears with Delete, widens a column a number no longer fits, and keeps one undo stack whose steps select what they changed. It shows the Formula Bar and Name Box, and speaks its words by id, in English or in Japanese (`japanese: true`), with a Consumer's `label` over either. `@ex-grid/sheet-react` mounts it as the `ExSheet` component. Neither package is published yet; Format Cells, Find and the toolbar follow.
