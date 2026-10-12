Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` at a rate below 0 is `#NUM!`, where it gave a number above -1 and, at -1 or below, for some arguments: Excel gave `#NUM!` at every negative rate it was asked, -0.5, -1, -1.5 and -2, and between -0.5 and 0 the `#NUM!` is ExSheet's own until Excel has been asked. A Formula that relied on a negative rate shows `#NUM!`.
