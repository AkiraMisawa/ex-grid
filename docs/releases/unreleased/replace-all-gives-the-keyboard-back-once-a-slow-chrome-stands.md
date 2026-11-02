Product: ExGrid
Kind: Fixed
ADR: 0332, 0410

A Replace All answered before a find panel's Chrome slot had drawn its buttons disabled, as Blazor's MudBlazor panel does a round trip away, gave the keyboard back to the button it lent from, which the late drawing then disabled: the keyboard fell to the page, and an Escape after it no longer closed the panel. The button now has the keyboard back once the slot's contents stand drawn from the answered view, and keys typed in between wait for it.
