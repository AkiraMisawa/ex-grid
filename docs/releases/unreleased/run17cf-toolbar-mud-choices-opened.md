Product: ExSheet
Kind: Added
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100

`ToolbarItemContext` gains `ChoicesOpened`, after `ChoicesClosing`: a Chrome whose list is its own calls it with true once the list it opened for `OpenRequest` holds the keyboard, and answers the keys it returns, which were typed on the toolbar while the list was on its way, as typed in the list, ahead of any typed there since; with false, for a list that did not open, the toolbar takes them back. `MudSheetChrome` does so; code that constructs a `ToolbarItemContext` itself passes the new parameter.
