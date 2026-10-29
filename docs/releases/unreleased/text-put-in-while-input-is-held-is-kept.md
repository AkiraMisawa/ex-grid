Product: ExGrid
Kind: Fixed
ADR: 0380

Text put into a field with no key of its own while the grid holds input behind an answer still out, an automation's fill or a paste from a menu, is now put in at its turn. Before, it was dropped without a word; this showed most on Blazor Server, where a filter panel's search typed by a test could come out empty. A drop or a spelling fix, whose place only the browser knew, is said lost instead.
