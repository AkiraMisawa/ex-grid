Product: ExPivot
Kind: Fixed
ADR: 0362

A Field List tick pressed straight after a command in a field's menu, while the menu was still taking the keyboard, no longer dismisses the menu ahead of that command. The command used to be lost, and the pivot then took no further input: the tick stayed unticked and the pivot never settled. The command now runs first and the tick turns after it, as when the two are pressed slowly. This was seen on Blazor Server, under both Chromes.
