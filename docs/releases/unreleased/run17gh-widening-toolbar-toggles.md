Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0071

The Sheet Toolbar's Underline and Strikethrough, and `SetCellFormatFromFocusAsync` that they set through, widen a column you have not sized whose numbers show `####`, as Ctrl+U and Ctrl+5 do, in the press's undo step. Bold and Italic still widen nothing, as Ctrl+B and Ctrl+I do not, and neither does the Font Colour, whose face and list set through `SetCellFormatAsync`.
