Product: ExSheet
Kind: Breaking
ADR: 0100

`ExSheet.Components.ExSheet`'s `ToolbarContent` is drawn by the Script Core: a `ToolbarButton`'s `OnClick` runs in .NET before the Sheet takes the next key, and its `ChildContent` is drawn from your markup (a Chrome that draws the item marks its place with the new `SheetScriptSlotAttributes.Content`, told by `SheetToolbarItemView.HasContent`). A Toolbar Item of your own derived from `ToolbarItemBase`, a KeyTip on one of ExSheet's items and a `ToolbarRow` without a `Label` are refused by name. The Script Core's `SheetHandle` and `@ex-grid/sheet-react`'s ref gain `tell(sentence)`, which says a sentence in the Sheet's notice.
