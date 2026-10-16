Product: ExGrid
Kind: Added
ADR: 0220

The grid's root carries `data-ex-settled` while every input its keyboard handling has taken (a key, a press on the rows, a paste, a composition, a copy) has been handled and painted, so a test or any script that drives the grid at machine speed waits for it and then reads, instead of waiting a fixed time; ExSheet and ExPivot carry it on the grid they are drawn with.
