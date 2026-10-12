Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0181

`LET` now takes what Excel's keyboard was seen to take, and which it refused on entry before: the name of a function, whether the engine has it or not (`=LET(IRR,1,IRR)`), an R1C1 shape that does not read as an A1 Reference (`=LET(RC,2,RC+1)`), and a value left empty (`=LET(x,,x)` shows 0). A name followed by `(` is still the function's call, and `R2`, `C3`, `TRUE` and `FALSE` are still refused.
