Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

Until Excel has been asked, these are `#VALUE!` where they gave a number or another Error Value: `XNPV` at a rate of exactly 0 whose values and dates are otherwise in order, where its last addition nearly cancels, so that Excel's adjustment of a last addition would give 0, and where an Error Value among its values or dates fails beside a rate below 0 or of 0, counts that differ, a date before the first or another Error Value, where it gave the first Error Value; `PV` at a rate of -1 with a number of periods above 0, where it gave `#NUM!`; and `PMT` at a rate of -1 with a number of periods other than 0, at a rate below -1 with a whole number of periods or a fraction whose reciprocal is odd, where a quantity some evaluation of it computes passes a double's range or falls below its normal numbers (a rate, a value or a growth near those edges, such as `=PMT(1,1020,1000)`), where 1+rate raised lies below 2^-52, and with no periods at the start of each period, where it gave a number, `#NUM!` or `#DIV/0!`. A Formula that relied on such arguments shows `#VALUE!`.
