Product: ExSheet
Kind: Fixed
ADR: 0240

Under ExSheet.MudBlazor, an Escape typed in More Colours on a circuit could close nothing and hold every input after it, when the keyboard had moved inside the popup just before — a press on its RGB/HSL switch. The surface's Escape no longer waits for a move of the keyboard that no handler of the Sheet hears.
