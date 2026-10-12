Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` now answer a number of periods that is not a whole number, or is negative, at a rate other than 0, where they were `#VALUE!`, and give the result Excel gives: 1+rate is raised as 1 over the power to a negative number of periods, by the square root at half a period, and as e^(n ln(1+rate)) otherwise. At a rate below -1, a number of periods whose reciprocal is an odd whole number is the odd root, and any other fraction `#NUM!`; at a rate of -1, no periods is `#NUM!` (`FV` gave the present value) and a negative number of periods `#DIV/0!`, as in Excel. `PV` at a rate of -1 with periods is `#VALUE!` until Excel has been asked. `PMT`, `PV` and `FV` read `type` as a truth value, so any number other than 0, 0.5 and 0.1 included, is payment at the start of each period, where a fractional `type` was `#VALUE!`. `PMT` with a number of periods that is not a whole number from 0 stays `#VALUE!`, though Excel answers it.
