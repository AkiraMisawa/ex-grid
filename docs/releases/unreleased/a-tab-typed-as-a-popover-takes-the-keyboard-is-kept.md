Product: ExGrid
Kind: Fixed
ADR: 0240

A key typed just after a popover took the keyboard goes to it. Until the next animation frame the grid still held keys for the popover, and a key it cannot hand on from script — Tab, Shift+Tab — was refused although the popover had the keyboard: Shift+Tab on Format Cells' first tab, or Tab in Find's field, now and then did nothing on Blazor Server. The hold now ends at the key that finds the keyboard arrived. ExSheet's and ExPivot's grids are fixed with it.
