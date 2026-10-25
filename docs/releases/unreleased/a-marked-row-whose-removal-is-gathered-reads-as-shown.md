Product: ExGrid
Kind: Fixed
ADR: 0043, 0141

`GridSource.From` with a Row Key now reads its Row Marks from the rows the grid shows while a removal is still being gathered. Before, the header's counts threw when a marked row's removal was applied and not yet shown, and a click on that row's checkbox was ignored. A mark that leaves with its row is now also reported to the marks' own `Changed` listeners, not only through the source's `StateChanged`.
