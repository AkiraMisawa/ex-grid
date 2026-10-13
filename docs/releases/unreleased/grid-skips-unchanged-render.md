Product: ExGrid
Kind: Behaviour
ADR: 0027

A render of the Consumer that hands the grid nothing new — the same Window holding the same rows where they are painted, the same Columns and the same delegates — no longer renders the grid at all, where before it rendered the grid's root once; restyling the element around the grid now costs no .NET render.
