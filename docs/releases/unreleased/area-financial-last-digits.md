Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` with payments at the start of each period give Excel's last digit where they were one unit in the last place below it: the payment now multiplies the product of 1+rate and the annuity factor, as Excel's does. With a present value as well, `FV` is `#VALUE!` where Excel's results leave two possible last digits.
