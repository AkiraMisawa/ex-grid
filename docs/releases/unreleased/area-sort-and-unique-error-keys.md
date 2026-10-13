Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

An Error Value among `SORT`'s keys that already follows every number is now sorted, where `SORT` used to be `#VALUE!`: over 7 and then `#N/A`, `=SORT(A1:A2)` spills 7 and then `#N/A`, and Excel's Anchor showed the same 7. With a number after it, beside text, a boolean, a blank or another Error Value, in a descending sort, and among `SORTBY`'s keys, the Formula is still `#VALUE!` until Excel has been asked where such a key goes, and `IFERROR`, `ISERROR` or `COUNTA` around it no longer turn that into an answer.
