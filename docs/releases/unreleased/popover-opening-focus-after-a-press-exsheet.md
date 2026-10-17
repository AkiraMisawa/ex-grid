Product: ExSheet
Kind: Fixed
ADR: 0204

In Format Cells, a choice pressed as the panel appears keeps the keyboard. On Blazor Server, a choice pressed at once and then OK with Enter applied nothing before, because the panel's opening focus landed after the press and the Enter went to the first tab (ticket 240).
