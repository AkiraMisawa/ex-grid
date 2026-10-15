Product: ExSheet
Kind: Breaking
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0201

`ToolbarItemContext` gains `ChoicesClosing`, after `ChoicesClosed`: a Chrome whose list is its own calls it as the list begins to close, with the key that closes it, and passes the keys typed in the list from then on to the action it returns, which the Sheet Toolbar hands on in order to where the keyboard lands; `ChoicesClosed` is now called once the list has gone, after a choice as well. `MudSheetChrome` does both; code that constructs a `ToolbarItemContext` itself passes the new parameter. The record's constructor and generated `Deconstruct` signature change: update construction and deconstruction sites for the new positional members and rebuild calling assemblies.
