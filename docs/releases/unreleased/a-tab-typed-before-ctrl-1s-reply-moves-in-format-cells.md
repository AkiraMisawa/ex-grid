Product: ExSheet
Kind: Fixed
ADR: 0240, 0071

A Tab or Shift+Tab typed once Format Cells' tab holds the keyboard, opened by Ctrl+1, moves inside Format Cells even when it is typed before the grid has had Ctrl+1's own reply. Before, such a key was held until that reply and then refused, so the keyboard stayed on the tab: at machine speed on WebAssembly, and on Blazor Server whenever the reply came after the tab took the keyboard. Any key a Consumer declares that opens its own contents in the grid's popover frame is fixed with it.
