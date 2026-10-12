Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`ERROR.TYPE` gives Excel's numbers for its newer Error Values, read from Excel: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13 and `#CALC!` 14, where it gave `#VALUE!`. `#GETTING_DATA` and `#CIRC!` are still given back, never numbered.
