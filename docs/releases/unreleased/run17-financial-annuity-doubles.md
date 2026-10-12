Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PMT`, `PV` and `FV` give Excel's result to the last digit, where they could differ from it in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring as Excel does, and `PMT` computes the payment beyond a double's precision before rounding it once, as the seventeenth Windows run found. A `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
