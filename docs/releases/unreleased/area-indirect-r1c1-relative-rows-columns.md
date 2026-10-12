Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` now reads R1C1 text (`a1` FALSE) in the forms Excel was observed to read, where it gave `#VALUE!`, and in those that follow from them: R and C each a number (`R3C2`, `R03C2`), an offset in brackets from the Formula's own cell (`R[1]C[-1]`, `R[0]`, `R[-0]`, `R[+1]`), or no number, the Formula's own row or column (in C2, `RC[-1]` is B2); a row part alone a whole row (`R3`, `R[1]`), a column part alone a whole column (`C2`); two parts of the same kind joined the rectangle they span (`R2:R3`, `RC[-1]:R[1]C[-1]`); a cell followed by `#` its Spill Range (`R1C1#`); and a Linked Table's column as text (`Positions[PV]`) as in A1 style. Parts of two kinds joined, `R2C2:R3` or `R3:C2`, name no place and are `#REF!`, as Excel's are, and so is a row or column number past the Sheet's edge. Still `#VALUE!` until Excel has told what they name: an offset past the Sheet's edge (`R[-5]C[-1]` from row 2, which Excel reads as a place, though not yet which), and a row or column number 0 (`R0C2`).
