Product: ExGrid
Kind: Fixed
Docs: exgrid/selection#driving-the-grid-from-a-script
ADR: 0240

A press made while an edit's commit waits for its verdict or its answer lands on the cell it was made on once the
commit is answered. It could land rows below that cell, out of view, because where it was made was read only once the
editor had closed, and a press on the editor itself could land on the grid's first cell. ExSheet and ExPivot, which
ExGrid draws, are fixed with it.
