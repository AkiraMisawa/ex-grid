Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`COTH`, `CSCH` and `SECH` answer an argument of 2^27 or more as Excel does, where they were `#NUM!` at the limit Microsoft documents: `COTH(2^27)` is 1, and `CSCH` and `SECH` of it are 0. `SIN`, `COS`, `TAN`, `COT`, `CSC` and `SEC` still give `#NUM!` there, as Excel was observed to.
