Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` gives the last digits Excel gives, where they differed by a few units in the last place: each value is now divided by 1+rate raised by squaring where its date is a whole number of years from the first, and as e^(years ln(1+rate)) with the exponential and the logarithm correctly rounded otherwise, where it was a correctly rounded power. Every result Excel was asked is now Excel's to the last digit.
