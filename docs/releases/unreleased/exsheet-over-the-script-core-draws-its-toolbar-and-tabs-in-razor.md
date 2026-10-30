Product: ExSheet
Kind: Added
ADR: 0410

An `ISheetChrome` handed to `ExSheet.Components.ExSheet` fills the Sheet's own slots, each its new `SheetScriptSlots` names: the Toolbar Items, drawn from a `SheetToolbarItemView` through the new `ToolbarItem` overload and marked with `SheetScriptSlotAttributes`, and the Sheet Tabs' faces and the + button's. A Toolbar Item's list opens, moves and closes in the browser, so its keys never wait on a circuit. `MudSheetChrome` fills all three. The Sheet Tabs' menu and the find panel stay the Script Core's own. In TypeScript, `@ex-grid/sheet` exports the keys of a Chrome-drawn Toolbar Item's list (`toolbarChoiceMovedTo`, `toolbarChoiceStart`, `toolbarChoiceRows`), which `useToolbarItem` now reads.
