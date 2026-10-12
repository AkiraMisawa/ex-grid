Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

An Error Value among `SORT`'s keys sorts after the numbers, as in Excel, where `SORT` used to be `#VALUE!`: over 7 and `#N/A`, `=SORT(A1:A2)` spills 7 and then `#N/A`. Beside text, a boolean, a blank or another Error Value, in a descending sort, and among `SORTBY`'s keys, it is still `#VALUE!` until Excel has been asked where it goes.
