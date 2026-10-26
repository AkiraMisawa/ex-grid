Product: ExGrid
Kind: Added
ADR: 0050, 0370

`@ex-grid/core`'s grid handle takes `openPopover(content, label)` and `closePopover()`, as the Blazor component's `OpenPopoverAsync` and `ClosePopoverAsync`: the Consumer's own contents in the grid's popover frame, placed under the header band and bounded by the grid's box, Tab wrapping inside it, and closed by Escape, a press elsewhere in the grid or the box shrinking below a row. `content` is handed a `GridPopoverContext` with `close` and `innerPopupChanged`. It answers `false`, opening nothing, while an edit is open.
