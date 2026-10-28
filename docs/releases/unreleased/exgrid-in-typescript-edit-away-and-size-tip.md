Product: ExGrid
Kind: Added
ADR: 0370, 0331, 0007, 0052

`@ex-grid/core` now keeps an edit open while another set of rows is shown, as the C# core does: a view handed with `showViewWith`, or rows never shown handed with the new `showNewView`, leaves the edit away, its commit landing on the row it was opened on, and `readEditText` reads its text. An edit not typed into closes once the Window no longer holds its row (ST-2), and a drag across Headings over more than one column or row shows the Size Tip.
