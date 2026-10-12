Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#arrays-that-spill
ADR: 0124, 0125

A Formula whose `INDIRECT` or `OFFSET` reads a cell of a Spill Range now follows that spill when it is blocked or freed; it used to keep the Value the cell had before. To see it, a recalculation computes every volatile Formula once more after each layout of the spills that changes something: twice in all when a change blocks or frees one spill, and only once when no spill moves.
