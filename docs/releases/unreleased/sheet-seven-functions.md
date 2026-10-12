Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0047, 0124, 0180, 0181

Seven more of Excel's functions answer as Excel does: `LET`, `RAND`, `RANDBETWEEN`, `SUBTOTAL`, `AGGREGATE`, `ERROR.TYPE` and `INDIRECT`, each with an Error Value, never another number, where Excel has not been asked. A `LET` name may be the name of a function, whether or not the Sheet has it, or an R1C1 shape that reads as no cell, and a `LET` value may be left empty, as Excel's keyboard was seen to take them; a name that reads as a cell, `TRUE` and `FALSE` are refused as the Formula is typed.
