Product: ExSheet
Kind: Added
ADR: 0100

ExSheet in TypeScript shows Excel's KeyTips on its Sheet Toolbar, as the Blazor Sheet does. The Alt key pressed and released alone, or F10, where the grid takes keys and no edit is open, takes the keyboard to the toolbar and shows the row's letter, H. H shows the items' letters, Excel's Home tab's (1 Bold, 2 Italic, 3 Underline, 4 Strikethrough, FC Font Colour, H Fill Colour, B Borders, AL, AC and AR the alignments, N Number Format, P Percent Style, K Comma Style, OE Format Cells). An item's letters press it, or open its list, so Alt, H, H opens the Fill's palette. Letters typed that narrow a level show only the letters they begin. Escape goes back one level, from a list a KeyTip opened to the row's letters and from the rows' letters to the Sheet, and any other key ends them.
