Product: ExSheet
Kind: Breaking
ADR: 0240
Docs: exsheet/formatting#the-sheet-toolbar

`ToolbarItemContext` no longer has `ChoicesClosing` or `ChoicesOpened`, and `ChoicesClosed` now takes the choice made and whether Escape closed the list: a Chrome's own toolbar list no longer passes the keys typed on the way to the toolbar or answers the keys it hands back. It calls `ChoicesClosed` in the turn of the key or press that closes it, while it still holds the keyboard, and the toolbar makes the choice itself; it takes the keyboard through the new `TakeKeyboard`, joins the Sheet's order through `JoinSurface` when it is drawn outside the Sheet, and puts its keys and presses in their turn through `InOrder`. A list asked to open that cannot calls `ChoicesClosed` with no choice. Under ExSheet.MudBlazor the toolbar's buttons and lists are in the Sheet's order as the built-in ones are: a press on an item, and a key or a choice in its list, is answered in its place, and what is typed after it goes where it leaves the keyboard.
