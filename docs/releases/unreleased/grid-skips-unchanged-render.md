Product: ExGrid
Kind: Behaviour
ADR: 0027

A render of the Consumer that hands the grid nothing new — the same Window, Columns and delegates, holding the same rows and columns as when the grid last rendered — no longer renders the grid at all, where before it rendered the grid's root once; restyling the element around the grid now costs no .NET render.
