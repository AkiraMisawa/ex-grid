Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#format-cells
ADR: 0071

Under `ExSheet.MudBlazor`, keys typed as Format Cells closes from Escape, OK or Cancel are the Sheet's, in order: on a Blazor Server circuit ↓ typed straight after Escape was lost, and now moves the Focus. OK sets what was touched as it is pressed, so a key typed after it changes nothing it sets.
