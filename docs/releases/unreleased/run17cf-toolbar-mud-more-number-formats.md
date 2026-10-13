Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0039, 0100

Under the built-in Chrome, More Number Formats… in the Sheet Toolbar's Number Format list opens Format Cells in the list's place, as Format Cells… does from the Context Menu: the grid is told the keyboard is going to Format Cells before it takes it back from the closing list. On a Blazor Server circuit the list was closed first, and a key typed in the moment between the two could reach the grid instead of Format Cells.
