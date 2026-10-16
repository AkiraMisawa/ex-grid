Product: ExGrid
Kind: Fixed
ADR: 0051, 0010, 0220

On Blazor Server, keys typed straight after Enter in the Name Box were lost: they went into the Name Box while the keyboard was on its way back to the grid, so `D10`, Enter, `abc` typed at once opened no edit, on ExSheet as on any ExGrid that shows the Formula Bar. The keys typed after the Name Box's Enter now wait until the grid has placed the Selection where the name says, and then reach the grid in order, so they open an edit on the cell named however fast they are typed (ticket 200).
