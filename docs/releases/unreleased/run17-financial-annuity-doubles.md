Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit, and a `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`. `PMT` no longer loses the last digits in 1+rate: where it answers, the payment at the end of each period is the exact payment rounded once, and the payment at the start that double divided by 1+rate and rounded again, which gives every payment the seventeenth run read and Excel's at a rate of 10^-7. 19 of the 30 payments Excel was asked differ from ExSheet's by 1 to 11 units in the last of a double's digits, a relative difference of at most about 1.3e-15 that reaches the 15th significant digit at three of them, which no evaluation found removes; this is listed under 0.1.0's Known limitations, and where the evaluations Excel's payments leave part, `PMT` is `#VALUE!` until Excel has been asked.
