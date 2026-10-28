Product: ExGrid
Kind: Added
ADR: 0370, 0331, 0007, 0052

`@ex-grid/core` now keeps an edit open while another set of rows is shown, as the C# core does: a view of other rows handed with `showViewWith`, or rows never shown handed with the new `showNewView`, leaves the edit away, its editor holding the keyboard unseen, Point pointing from the Focus of the rows shown, and its commit landing on the row it was opened on. `readEditText` reads the open edit's text. An edit opened on the cell's own text and not typed into closes, as Escape does, once the Window no longer holds its row (ST-2), and a drag across Headings over more than one column or row shows the Size Tip, the Name Box empty meanwhile.

`placeSelection` no longer scrolls the page to bring the placed Focus into the window: a Consumer's placement, a Name Box's Go To among them, scrolls the grid alone, as the C# core's does (ADR-0204), so a page that was where the user left it stays there.

In Caret, in the Cell Editor or the Formula Bar, `@ex-grid/core` now takes PageUp and PageDown and does nothing with them, and on macOS answers Home and End itself, placing the caret or extending the selection, as the C# grid does (ADR-0010, ticket 32). Left to the browser, they could scroll the grid away from the open edit (Edge did with PageUp and PageDown).
