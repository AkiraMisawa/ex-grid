Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A blank among `SORT`'s and `SORTBY`'s keys sorts after every other key, in either order, and in `UNIQUE`'s array is one of a kind only with another blank, never with 0; each spills as 0, and `ROWS` and `COLUMNS` count it, as in Excel, where all three used to be `#VALUE!`. Read inside a Formula, by another function, an operator or `LET` (`=COUNTA(UNIQUE(A1:A3))`), a blank anywhere in their result makes the Formula `#VALUE!`, which `IFERROR` does not catch, until Excel has been asked what it reads, a blank in a column they do not sort by included, which such a Formula used to read; an Error Value among `SORT`'s and `SORTBY`'s keys is still `#VALUE!`.
