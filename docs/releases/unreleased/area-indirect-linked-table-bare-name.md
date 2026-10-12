Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT("Positions")`, a declared Linked Table's bare name, now reads the table's data as Excel reads it, where it gave `#VALUE!`: every column in the table's order and every row, without the header, so `ROWS(INDIRECT("Positions"))` counts the rows, `INDEX(INDIRECT("Positions"),2,1)` reads the second row's first Value, and the name as a Formula's result spills the whole table. It waits while the table does, `#GETTING_DATA` even under `IFERROR`, and is `#VALUE!` over a snapshot of no rows. Typed, a table's bare name is still `#NAME?`; a column is read by name, `Positions[PV]`, as before.
