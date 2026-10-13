Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`SMALL` truncates a k that is not whole, as Excel does: `SMALL(A1:A9,1.5)` is the smallest, where it used to be `#VALUE!`, and `AGGREGATE`'s 15 reads its k the same way. A k below 1 or above the count of the numbers is `#NUM!` for `SMALL` and `LARGE` alike, whole or not, before it is rounded, so `LARGE(A1:B5,0.5)` and `SMALL(A1:A9,9.5)` over nine numbers, which were `#VALUE!`, are `#NUM!` as in Excel; no k is `#VALUE!` any more.
