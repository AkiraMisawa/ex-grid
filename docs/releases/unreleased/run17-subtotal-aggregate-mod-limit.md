Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`MOD` answers a quotient of 2^27 or more, as Excel 365 does (`MOD(2^27,1)` is 0), where it used to be `#NUM!`. It is `#NUM!` from a quotient of 1,125,900,000,000, the limit a Microsoft 365 user has reported for Excel.
