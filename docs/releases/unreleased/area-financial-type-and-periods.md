Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` now answer a number of periods that is not a whole number, or is negative, at a rate other than 0, where they were `#VALUE!`, and give Excel's result where Excel was asked (10.5 and -10 periods). Where Excel's results leave two ways of raising 1+rate that give different results, they stay `#VALUE!` until Excel has been asked: half a period (the square root, or e^(0.5 ln(1+rate))), a negative number of periods that is not a whole number (the power, or 1 over the power to its absolute value), a negative number of periods at a rate of -1, and one that is not a whole number at a rate below -1. `PMT`, `PV` and `FV` read `type` as a truth value, so any number other than 0, 0.5 included, is payment at the start of each period, where a fractional `type` was `#VALUE!`; `PMT` with a number of periods that is not a whole number from 0 stays `#VALUE!`, though Excel answers it.
