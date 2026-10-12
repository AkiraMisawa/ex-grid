Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0039

Under `ExSheet.MudBlazor`, a Sheet Toolbar list opened from the keys — a KeyTip such as Alt, H, F, C, Enter on Number Format, ↓ on a split control — takes the keyboard into its choices, as the built-in Chrome's does, and answers the built-in list's keys: the arrows, Home and End move, swatches ten across; Enter or Space chooses; Escape closes the list and gives the keyboard back to the Sheet. Before, the keyboard stayed on the toolbar, Escape left the list standing, and the next list opened beside it.
