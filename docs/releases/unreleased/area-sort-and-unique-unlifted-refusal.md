Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A function given a range or an array where it takes one Value, which Excel 365 applies to each Value and ExSheet does not yet, now makes the whole Formula `#VALUE!` wherever it stands: `=IFERROR(LEN(A1:A3),0)` is `#VALUE!` where it used to be 0, and `ISERROR`, `ISNUMBER`, `COUNTA` and `COUNT` around such a call no longer answer TRUE, FALSE, 1 or 0 about the `#VALUE!` the engine made in place of what Excel computes.
