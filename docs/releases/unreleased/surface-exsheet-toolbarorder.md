Product: ExSheet
Kind: Breaking
ADR: 0201

`ExSheet.ToolbarOrder` is no longer public, and neither is the `Order` cascading parameter of `ToolbarItemBase`, `ToolbarRow` and `DefaultToolbarRow` that carried it: it is the bookkeeping that keeps the Sheet Toolbar's items in markup order (ADR-0100). A Consumer's items and rows still take their places in markup order, and have nothing to write.
