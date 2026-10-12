Product: ExSheet
Kind: Behaviour
Docs: exsheet/functions
ADR: 0120

`TEXT` shows a number in `General` as a cell's General shows it in 11 characters, and a number under an empty code as empty text, as Excel does: `TEXT(1/3,"General")` is `0.333333333` and `TEXT(5,"")` is empty, where both gave `#VALUE!`, and `TEXT(0.00001,"General")` is `0.00001`, where it gave `1E-05`. A negative number whose minus sign would cost it a digit, and a number under a lone `@` whose fitted General is not its General in full, are `#VALUE!` until Excel is asked.
