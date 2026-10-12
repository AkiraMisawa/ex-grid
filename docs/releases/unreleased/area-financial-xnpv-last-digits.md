Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` gives the last digits Excel was seen to give, where they differed by a few units in the last place: each value is now discounted by e^(years ln(1+rate)), where it was a correctly rounded power. Excel was asked two results, and other evaluations give both of them too; at the dates where those part from this one, ExSheet's last digits may still differ from Excel's. That discount has no answer at a rate of -1 or below, where `XNPV` is now `#VALUE!` until Excel has been asked.
