Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050

A key typed while the keyboard is on its way back from the Sheet Toolbar reaches the Sheet, in order: Enter straight after a KeyTip's command, after Enter on an item, or after a choice in a list moves the Focus, and Escape straight after Alt, H, O, E closes Format Cells. On a Blazor Server circuit it pressed the last toolbar item again, or chose the same colour twice. Under `MudSheetChrome`, a key typed in a list in the round trip after a choice or Escape is still the list's, and is dropped.
