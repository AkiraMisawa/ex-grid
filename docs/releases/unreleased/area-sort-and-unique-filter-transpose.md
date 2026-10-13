Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A blank `FILTER` or `TRANSPOSE` keeps from its array is now read inside a Formula as a blank `SORT` keeps is, which Excel has not yet been asked about for these two: `=COUNTA(FILTER(A2:A9,B2:B9="x"))` counts it, where it used to pass over it although `=COUNTA(SORT(FILTER(A2:A9,B2:B9="x")))` counted it, and a function whose readings of it part, such as `AVERAGE`, `MIN`, `COUNT`, `MATCH`, `IF` or another of the array functions, makes the Formula `#VALUE!` where it used to answer, so a Formula over a filtered range with a blank in it may now show `#VALUE!`. `VLOOKUP`, `HLOOKUP` and `XLOOKUP` over such an array answer wherever the blank is neither a key they search nor what they return.
