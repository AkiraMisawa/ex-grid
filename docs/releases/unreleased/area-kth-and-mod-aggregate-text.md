Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`AGGREGATE`'s array form (`LARGE` and `SMALL`, 14 and 15) takes text or a boolean given directly as an array holding no number, as Excel does: `AGGREGATE(14,6,"5",1)` is `#NUM!`, where it was `#VALUE!`. A number given directly is still that one number.
