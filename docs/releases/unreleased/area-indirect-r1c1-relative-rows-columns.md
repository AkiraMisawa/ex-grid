Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` now reads R1C1 text (`a1` FALSE) that is relative or names whole rows or columns, as Excel was observed to read it, where it gave `#VALUE!`: in C2, `INDIRECT("R[1]C[-1]",FALSE)` is B3 and `RC[-1]` the cell to its left, `R3` and `R[1]` are whole rows, `C2` and `C[-1]` whole columns, and `R2:R3` or `RC[-1]:R[1]C[-1]` the rectangle they span. An offset past the Sheet's edge is `#REF!` until Excel has been asked whether it wraps round to the last rows. A number written with a leading 0 or a `+`, a cell joined to a whole row or column or a row to a column, a Linked Table's column or a Spill Range written in R1C1 style, and a Linked Table's bare name are still `#VALUE!`.
