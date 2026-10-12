Product: ExGrid
Kind: Behaviour
ADR: 0027

A render of the Consumer that hands the grid no new parameter — the same Window, Columns and delegates as before, for instance while the page restyles the element around the grid — no longer renders the grid at all, where before it rendered the grid's root once (rows were already skipped). A Consumer that relied on such a render to repaint should hand the grid what changed.
