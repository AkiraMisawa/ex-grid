Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit, and a `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`. `PMT` computes the payment exactly and rounds it once, which gives every payment the seventeenth run read and Excel's at a rate of 10^-7, but 19 of the 30 payments Excel was asked differ from it by 1 to 11 units in the last of a double's digits, a relative difference of about 1e-15 that no format displays and no evaluation found removes, listed under 0.1.0's Known limitations.
