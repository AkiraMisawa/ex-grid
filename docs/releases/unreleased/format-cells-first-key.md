Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#format-cells

Under `ExSheet.MudBlazor`, a key typed as Format Cells opens is no longer lost: MudBlazor's focus trap took the keyboard to an empty element of its own before the dialog's tabs did, because MudBlazor 9 ignores a first focus set only in the dialog's options (#70).
