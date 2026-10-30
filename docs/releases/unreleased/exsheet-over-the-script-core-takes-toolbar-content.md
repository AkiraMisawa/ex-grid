Product: ExSheet
Kind: Added
ADR: 0100

`ExSheet.Components.ExSheet` now takes `ToolbarContent`, declared as on the Razor component: `ToolbarRow`s, `DefaultToolbarRow`, ExSheet's Toolbar Items, `ToolbarSeparator`s and your `ToolbarButton`s. The Script Core draws the toolbar; a button's `OnClick` runs in .NET before the Sheet takes the next key, and its `ChildContent` is drawn from your markup (a Chrome that draws the item marks its place with the new `SheetScriptSlotAttributes.Content`, told by `SheetToolbarItemView.HasContent`). A Toolbar Item of your own derived from `ToolbarItemBase`, a KeyTip on one of ExSheet's items and a `ToolbarRow` without a `Label` are refused there by name, as are `OpenAsync` and `SaveAsync` called from a button's `OnClick`, whose press holds the Input Order they would wait for. The Script Core's `SheetHandle` and `@ex-grid/sheet-react`'s ref gain `tell(sentence)`, which says a sentence in the Sheet's notice.
