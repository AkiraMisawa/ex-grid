Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` at a rate below 0 is `#NUM!`, as Excel's is, where it gave a number above -1 and, at -1 or below, a number for some arguments (every date the first, or dates whole years apart). A Formula that relied on a negative rate shows `#NUM!`.
