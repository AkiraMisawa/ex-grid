Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`PV` and `FV` give Excel's last digit where they were one unit in the last place from it: the payment now multiplies the product of 1+rate and the annuity factor, which gives Excel's doubles, and with a present value as well `FV` adds the two products as written. Where 1+rate is raised to a number of periods that is not whole, ExSheet computes the exponential and the logarithm correctly rounded, so a result is the same on every platform; at the one argument observed where the platform's exponential and the correctly rounded one differ, Excel's result was the correctly rounded one's, and no observed result tells logarithms apart.
