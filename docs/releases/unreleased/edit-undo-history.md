Product: ExGrid
Kind: Fixed
Docs: exgrid/editing#bulk-changes--delete-and-undo
ADR: 0380

Ctrl+Z and Ctrl+Y in an open edit undo and redo through the edit's own history, kept by the grid over the Cell Editor and
the Formula Bar alike: characters typed one after another are undone together, and a deletion, a paste, a moved caret or
a Reference pointed at is a step of its own. The field's own history ended its run whenever anything else on the page
repainted, so typed at machine speed, Ctrl+Z could stop short of where it stopped typed one key at a time.
