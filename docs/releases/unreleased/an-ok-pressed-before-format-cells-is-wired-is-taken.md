Product: ExSheet
Kind: Fixed
ADR: 0410

Format Cells drawn with Razor over the Script Core (the MudBlazor Wrapper's) no longer refuses an OK pressed in the round trip after a choice that adds a field, such as Percentage's Decimal places, on the Server host: the field stood empty until .NET said it had drawn it, so the OK was refused as a field left blank and Format Cells stayed open. The OK now waits for that drawing to be wired, and sets the choice.
