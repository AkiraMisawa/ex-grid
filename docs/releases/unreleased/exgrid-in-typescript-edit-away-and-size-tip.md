Product: ExGrid
Kind: Added
ADR: 0370, 0331, 0007, 0052

`@ex-grid/core` now keeps an edit open while another set of rows is shown, as the C# core does: a view of other rows handed with `showViewWith`, or rows never shown handed with the new `showNewView`, leaves the edit away, its editor holding the keyboard unseen, Point pointing from the Focus of the rows shown, and its commit landing on the row it was opened on. `readEditText` reads the open edit's text. An edit opened on the cell's own text and not typed into closes, as Escape does, once the Window no longer holds its row (ST-2), and a drag across Headings over more than one column or row shows the Size Tip, the Name Box empty meanwhile.
