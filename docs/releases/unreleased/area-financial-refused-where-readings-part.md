Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

Until Excel has been asked, these are `#VALUE!` where they gave a number or another Error Value: `XNPV` at a rate of exactly 0 whose values and dates are otherwise in order, and where its last addition nearly cancels, so that Excel's adjustment of a last addition would give 0; `PV` at a rate of -1 with a number of periods above 0, where it gave `#NUM!`; and `PMT` at a rate of -1 with a number of periods above 0, at a rate of -2 with an even number of periods, where 1+rate raised is exactly 1, and where 1+rate raised passes a double's range, where it gave a number or `#NUM!`. A Formula that relied on such arguments shows `#VALUE!`.
