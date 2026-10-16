Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050, 0039

On Blazor Server, the Sheet Toolbar's KeyTips could lose their order when an Escape was among the letters typed straight after F10 or Alt: F10, H, Escape, H, A could end the KeyTips at the A, typed on the toolbar as it took the keyboard, and the H waiting behind the Escape then opened an edit in the cell. The letters waiting for the KeyTips now reach them, in order, ahead of any typed on the toolbar, so F10, H, Escape, H, A, C centres the cell however fast it is typed.
