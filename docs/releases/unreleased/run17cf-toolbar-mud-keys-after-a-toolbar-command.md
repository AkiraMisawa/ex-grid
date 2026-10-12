Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050

A key typed while the keyboard is on its way back from the Sheet Toolbar, or out of one of its lists, reaches where the keyboard lands, in order: Enter straight after a KeyTip's command, after Enter on an item, or after a choice in a list moves the Focus, and Escape straight after Alt, H, O, E closes Format Cells. On a Blazor Server circuit it pressed the last toolbar item again or chose the same colour twice, and under `ExSheet.MudBlazor` a key typed in a list as it closed was dropped.
