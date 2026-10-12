Product: ExSheet
Kind: Behaviour
Docs: exsheet/functions#aggregation
ADR: 0047

`LARGE` rounds a k that is not whole up, as Excel does: `LARGE(A1:B5,1.5)` is the 2nd largest, where it used to be `#VALUE!`. `SMALL`'s k that is not whole, and `LARGE`'s between 0 and 1, stay `#VALUE!` until Excel has been asked; a k a hair from a whole number, such as `1+2^-52`, is now that number in both.
