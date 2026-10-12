Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` gives Excel's last digits where they differed by a few units in the last place: each value is discounted by e^(years ln(1+rate)), as Excel's is, where it was a correctly rounded power.
