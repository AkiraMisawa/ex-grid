Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`AND`, `OR` and `XOR` pass over text typed as an argument that spells neither TRUE nor FALSE, as they pass over text in a range, so `=AND("abc",TRUE)` is TRUE, as in Excel, where it was `#VALUE!`. With nothing else that counts, the result is still `#VALUE!`.
