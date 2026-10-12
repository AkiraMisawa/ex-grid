Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that rests on payments at rates of 0.5% to 10% a period; at much smaller rates Excel has not been asked, and its payment may differ from the 13th significant digit. A `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
