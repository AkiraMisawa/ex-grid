Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0124

`INDIRECT` reads the forms Excel was observed to read beside A1-style text: R1C1 text naming a cell or a range (`INDIRECT("R3C2",FALSE)`), a Linked Table's column as text (`INDIRECT("Positions[PV]")`, which waits while the table does) and a Spill Range (`INDIRECT("A1#")`). A declared Linked Table's bare name (`INDIRECT("Positions")`) is `#VALUE!`, where Excel reads the table's data, since a Linked Table is read by key, never by position; text naming a table or a column that does not exist is `#REF!`.
