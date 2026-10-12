Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100

On the Sheet Toolbar, Format Cells' KeyTip is O, E, as Excel's Home tab reaches Format Cells… through its Format menu: Alt, H, O shows Format Cells' KeyTip alone, and E opens it. Alt, H, O no longer opens Format Cells by itself, and an E typed after it no longer lands in the dialog. A KeyTip O of your own beside `FormatCellsItem` is now refused at render as colliding with its OE.
