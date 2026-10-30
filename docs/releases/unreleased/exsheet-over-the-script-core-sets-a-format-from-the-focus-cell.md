Product: ExSheet
Kind: Behaviour
ADR: 0100

`ExSheet.Components.ExSheet`'s `SetCellFormatFromFocusAsync` makes your change in .NET from the Focus cell's Cell Format as the browser read it, and sets it on the Selection. If the Selection moves, or the Focus cell's format changes, before the change reaches the browser, the change is refused with the new `SheetRefusalReason.SelectionMoved` or `SheetRefusalReason.FocusFormatChanged`, and the Sheet's notice says so. Nothing is ever set on cells the change was not made for. The Script Core's `SheetHandle` and `@ex-grid/sheet-react`'s ref gain `setCellFormatFromFocus`, which makes the change within the Sheet's turn, and `readFocusFormat` with `setCellFormatFrom` for a change made outside it.
