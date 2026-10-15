Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0071, 0100

The Sheet Toolbar's Font Colour, by its face or a colour from its list, now widens a column you have not sized whose numbers show `####`, in the press's undo step, as Excel's Home tab Font Color button did when read on Windows (2026-10-14). It sets the colour through `SetCellFormatFromFocusAsync`, which now widens for a Font colour too, as Excel's Home tab buttons do; `SetCellFormatAsync` still widens no column for a colour, as Excel's `Font.Color` set from code does not. A column you sized never widens.
