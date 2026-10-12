Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` at a rate of -1 or below is now `#VALUE!` until Excel has been asked, where it gave a number for some arguments (every date the first, or dates whole years apart at a rate below -1) and `#NUM!` for the rest: 1+rate has no logarithm there, so the discount Excel was seen to take has no answer, and the power taken before is not known to be Excel's. A Formula that relied on such a rate shows `#VALUE!`.
