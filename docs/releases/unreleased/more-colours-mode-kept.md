Product: ExSheet
Kind: Fixed

Under `MudSheetChrome`, Format Cells' More Colours keeps the mode the user switches to (RGB, HSL or HEX) while its picker is open. A switch pressed soon after the picker opened was taken back to HEX by the dialog's next render, so the picker seemed to ignore the press (ticket 260).
