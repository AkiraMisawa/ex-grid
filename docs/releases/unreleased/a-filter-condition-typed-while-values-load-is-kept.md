Product: ExGrid
Kind: Fixed
ADR: 0009

A filter panel shows its condition form while its value list is still loading. A condition chosen or typed there in that time now stays when the list arrives, and so does the keyboard. Before, the panel switched to the value list and the typed text went with the form, which happened mostly on Blazor Server, where the list is a round trip away.
