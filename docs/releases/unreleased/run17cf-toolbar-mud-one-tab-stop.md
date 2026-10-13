Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100

Under `MudSheetChrome` the Sheet Toolbar is one Tab stop, as under the built-in Chrome: Number Format's button and the arrows of Font Colour, Fill and Borders no longer take Tab. They are drawn by the Chrome itself now, out of the Tab order, and open a MudMenu that draws no button of its own.
