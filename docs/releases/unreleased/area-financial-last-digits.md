Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` give Excel's last digit where they were one unit in the last place from it: the payment now multiplies the product of 1+rate and the annuity factor, as Excel's does, and with a present value as well `FV` takes the sum as Excel writes it. Where 1+rate is raised to a number of periods that is not whole, the exponential and the logarithm are now correctly rounded, the same on every platform, as Excel's are where the platform's differ.
