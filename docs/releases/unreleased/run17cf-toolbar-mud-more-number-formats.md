Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0039, 0100, 0050

Under the built-in Chrome, More Number Formats… in the Sheet Toolbar's Number Format list opens Format Cells in the list's place, as Format Cells… does from the Context Menu: the grid is told the keyboard is going to Format Cells before it takes it back from the closing list. On a Blazor Server circuit the list was closed first, and a key typed in the moment between the two could reach the grid instead of Format Cells. Under `ExSheet.MudBlazor` the hand-off to Format Cells' dialog was told while the toolbar's closing list still held the keyboard, and so taken as reached there: Escape typed straight after More Number Formats… went to the grid and Format Cells stayed open. It is now told once the toolbar has given the keyboard back through the grid, and the keys handed back go on to Format Cells. The dialog's first render also follows that hand-back, so its focus cannot overtake the keyboard and keys returning from the toolbar.
