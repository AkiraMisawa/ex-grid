Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`FV` with both a present value and a payment is now `#VALUE!` where Excel's results leave two evaluations that give different last digits (the sum as it is, or with one of its products fused into it), at the start of each period or with a number of periods that is not a whole number from 0; at the end of each period with a whole number of periods it answers as it did, until Excel has been asked. `XNPV` is now `#VALUE!` where its dates are whole years apart and e^(years ln(1+rate)), squaring and a power give different last digits, and where the years, the days divided by 365 or multiplied by 1/365, differ and change the result. A Formula that relied on such arguments shows `#VALUE!`.
