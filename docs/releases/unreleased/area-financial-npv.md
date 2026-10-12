Product: ExSheet
Kind: Breaking
Docs: exsheet/formulas#supported-functions
ADR: 0202

`NPV` is no longer supported and is `#NAME?`, as `IRR` is: Excel's results differ from ExSheet's in their last digits, and no evaluation found gives every result Excel was seen to give. A Formula that calls `NPV` shows `#NAME?`; `XNPV` is unchanged.
