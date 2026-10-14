Product: ExSheet
Kind: Breaking
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0201

`ToolbarItemContext` gains `CloseRequest`, beside `OpenRequest`: it changes whenever the Sheet Toolbar takes the keyboard back while the item's list may be open from the keyboard, and takes the value `OpenRequest` had then; a Chrome whose list is its own closes the list when it changes, drops a focus into it not yet given, and calls no `ChoicesClosed` for it unless the list already holds the keyboard. It is a positional parameter of the record, after `OpenRequest`: code that constructs a `ToolbarItemContext` itself passes it. The record's constructor and generated `Deconstruct` signature change: update construction and deconstruction sites for the new positional members and rebuild calling assemblies.
