Product: ExGrid
Kind: Added
ADR: 0012, 0051, 0080

`@ex-grid/core`'s grid handle gains `focus()`, which hands the keyboard back to the grid (to an open edit's field where one stands), for a Consumer's own control beside the grid, and `keyName(event)`, which names a key press as the grid names its own keys, Command standing for Control on an Apple platform. `formulaBarHeight(density, rowHeight?)` answers the Formula Bar's height, so a Consumer can draw a band of its own at the same height, as ExSheet's tabs are.
