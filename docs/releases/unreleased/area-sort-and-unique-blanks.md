Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0125

A blank among `SORT`'s and `SORTBY`'s keys sorts after every other key, in either order, and in `UNIQUE`'s array is one of a kind only with another blank, never with 0; each spills as 0, as in Excel. All three used to be `#VALUE!`; an Error Value among `SORT`'s and `SORTBY`'s keys still is, until Excel has been asked.
