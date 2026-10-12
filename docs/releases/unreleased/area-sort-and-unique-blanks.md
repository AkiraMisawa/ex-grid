Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A blank among `SORT`'s and `SORTBY`'s keys sorts after every other key, in either order, and in `UNIQUE`'s array is one of a kind only with another blank, never with 0, and spills as 0, where all three used to be `#VALUE!`: what Excel was seen to do, `SORTBY`'s descending sort and spilled blank excepted, which are answered as `SORT`'s. Read inside a Formula, a blank the three keep is counted by `COUNTA` and taken out by `INDEX` as a blank, as Excel was seen to do for `SORT` and `UNIQUE` (`=COUNTA(UNIQUE(A1:A3))` counts a blank among the values as one); other functions that take one Value read it as a blank cell, as only `ISBLANK` was seen to, `SUM`, `SUMPRODUCT` and `CONCAT` take it because every reading of it gives them the same answer, and a function whose readings of it part, such as `AVERAGE`, `MIN` or `COUNT`, makes the Formula `#VALUE!`, which `IFERROR` does not catch, a blank in a column they do not sort by included, which such a Formula used to read as a blank cell.
