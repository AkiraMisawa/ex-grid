Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#arrays-that-spill
ADR: 0047, 0125

`INDEX` over a range of several rows and several columns with `column_num` left out, `=INDEX(G1:H5,2)`, is `#REF!`, as Excel's is; it used to spill the whole row. A Formula that wants the row writes `=INDEX(G1:H5,2,0)`.
