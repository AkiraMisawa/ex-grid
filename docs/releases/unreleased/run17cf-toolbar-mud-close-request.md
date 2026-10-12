Product: ExSheet
Kind: Added
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100

`ToolbarItemContext` gains `CloseRequest`, beside `OpenRequest`: it changes whenever the Sheet Toolbar takes the keyboard back while the item's list may be open from the keyboard, and takes the value `OpenRequest` had then. A Chrome whose list is its own closes the list when it changes, drops a focus into it not yet given, and does not call `ChoicesClosed` for that close; `MudSheetChrome` and the built-in Chrome do, so Alt, H, F, C, Escape typed at once closes the list with the keyboard on the Sheet, on a Blazor Server circuit too. It is a positional parameter of the record, after `OpenRequest`: code that constructs a `ToolbarItemContext` itself passes it.
