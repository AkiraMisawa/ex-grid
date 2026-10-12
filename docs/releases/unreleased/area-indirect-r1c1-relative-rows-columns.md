Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` now reads R1C1 text (`a1` FALSE) that counts from the Formula's own cell or names whole rows or columns, where it gave `#VALUE!`: the three forms Excel was observed to read (in C2, `R[1]C[-1]` is B3, `R3` row 3 and `C2` column B) and, until Excel is asked them, the forms that follow from those by the rule they settle — R and C each a number or an offset in brackets (`R3C[-1]`, `R[-1]C[-1]`), `R[1]` and `C[-1]` a whole row and column, and `R2:R3` or `R[1]C[-1]:R[2]C[-1]` the rectangle two parts of the same kind span. R1C1 text in a form those leave open stays `#VALUE!` until Excel has been asked — an R or a C with no number (`RC[-1]`, `R[2]C`), a zero offset in brackets (`R[0]`, `R[-0]`), a number written with a leading 0 or an offset with a `+`, an offset past the Sheet's edge (which Excel might call no place or wrap round to the last rows), a cell joined to a whole row or column or a row to a column, and structured or `#` text in R1C1 style — as does a declared Linked Table's bare name, while a row or column number past the Sheet's edge is `#REF!`, as `XFE1` is.
