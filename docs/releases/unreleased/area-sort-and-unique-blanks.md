Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A blank among `SORT`'s and `SORTBY`'s keys sorts after every other key, in either order, and in `UNIQUE`'s array is one of a kind only with another blank, never with 0; each spills as 0, as in Excel, where all three used to be `#VALUE!`. Read inside a Formula, a blank any of the three keeps is read as Excel reads it — `COUNTA` counts it (`=COUNTA(UNIQUE(A1:A3))` counts a blank among the values as one), `INDEX` takes it out as a blank, and operators and functions that take one Value read it as a blank cell — while a function Excel has not been asked about, such as `AVERAGE`, `MIN` or `COUNT`, makes the Formula `#VALUE!`, which `IFERROR` does not catch, a blank in a column they do not sort by included, which such a Formula used to read as a blank cell.
