Product: ExGrid
Kind: Fixed
ADR: 0018
Docs: none

On Blazor Server, a grid nested in another grid's cell, pressed straight after a key that opened an edit in the outer grid, lost the keyboard back to that edit when the edit's focus arrived a round trip later, and the keys typed next went into the outer edit. The outer grid now grants its edit, its popovers and its hand-backs the keyboard only while DOM focus is its own, not a nested grid's (ADR-0018). ExSheet, which can hold such a grid, gets the same.
