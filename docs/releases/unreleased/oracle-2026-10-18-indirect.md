Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` now answers as Excel does for the R1C1 and `#` forms it gave `#VALUE!` until Excel was asked. In R1C1 style a row or column number 0 or past the Sheet's edge (`R0C2`, `R1048577C2`) is `#REF!`; an offset past the Sheet's edge wraps round to the far side (from C2, `R[-5]C[-1]` is B1048573); a whole column joined to a cell (`C3:R2C2`) is `#REF!`; and parts joined by `:` are joined left to right, so `R2:R3C2:C3` is B2:C3, where it was `#REF!`. In either style, `#` after a range, a row or a column (`A1:A2#`, `R3#`) or inside a join (`A1#:A2`) is `#REF!`. A Formula that read these forms was refused before and now has an Error Value it can catch, or a Value.
