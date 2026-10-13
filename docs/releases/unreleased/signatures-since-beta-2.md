Product: ExGrid
Kind: Breaking
ADR: 0130, 0141, 0204

Some public signatures grew an optional parameter since `0.1.0-beta.2`, so code compiled against it is compiled again, and a positional deconstruction takes one more variable: `GridSource.Fetch` takes `summarize`, `rowKey` and `clock`, and the Chrome's `ColumnMenuContext`, `ContextMenuContext`, `FilterPanelContext` and `FindContext` take `TakeFocus`. Code that calls them by name or position compiles unchanged. `EditDiscardReason`'s values each moved down one when `OrderChanged` left (#70), so a reason stored as a number is read again.
