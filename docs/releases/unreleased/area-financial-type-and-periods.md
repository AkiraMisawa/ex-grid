Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` raise 1+rate to a number of periods that is not a whole number, or is negative, where they took the platform's power: as 1 over the power to a negative number, by the square root at half a period, as e^(n ln(1+rate)) otherwise, a negative 1+rate to its odd root where 1/n is odd and to any other fraction `#NUM!`, and at a rate of -1 `#NUM!` for no periods and `#DIV/0!` for a negative number, which are the results Excel gave for them wherever it was asked. At such a number of periods and a rate above -1, `PMT` gives the exact payment at the end of each period rounded once, and at the start that double divided by 1+rate and rounded again, where it does not refuse with `#VALUE!`; below -1, a fraction whose reciprocal is not odd is `#NUM!`. `type` is read as a truth value, so any number other than 0, 0.5 and 0.1 included, is payment at the start of each period, where a `type` other than 0 or 1 was `#VALUE!`.
