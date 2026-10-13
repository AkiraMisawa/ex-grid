Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#borders
ADR: 0206

Lines inside the Selection stay as drawn in a Pinned Column and across its edge, as they already did elsewhere: under a coloured shade, such as ExSheet.MudBlazor's or a `--ex-selection-fill` of your own, a black line there had taken the shade's colour, and a Fill had been tinted rather than darkened.
