Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` reads the forms Excel was observed to read beside A1-style text: R1C1 text naming a cell or a range (`INDIRECT("R3C2",FALSE)`), a Linked Table's column as text (`INDIRECT("Positions[PV]")`, which waits while the table does) and a Spill Range (`INDIRECT("A1#")`). R1C1 text that is relative or names whole rows or columns is `#VALUE!` until Excel is asked, and text naming a table or a column that does not exist is `#REF!`.
