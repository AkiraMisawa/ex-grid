Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0047, 0124, 0180, 0181

Seven more of Excel's functions answer as Excel does: `LET`, `RAND`, `RANDBETWEEN`, `SUBTOTAL`, `AGGREGATE`, `ERROR.TYPE` and `INDIRECT`, each with an Error Value, never another number, where Excel has not been asked. A `LET` name cannot be the name of any of Excel's functions, whether or not the Sheet has that function.
