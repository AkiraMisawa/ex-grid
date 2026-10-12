Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` now answer any number of periods at a rate other than 0, as Excel does: a fractional one raises 1+rate as e^(nper ln(1+rate)) and a negative one is 1 over the power, each giving Excel's double where they were `#VALUE!`. `PMT`, `PV` and `FV` read `type` as a truth value, so any number other than 0, 0.5 included, is payment at the start of each period, where a fractional `type` was `#VALUE!`. `PMT` with a number of periods that is not a whole number from 0 stays `#VALUE!`, though Excel answers it.
