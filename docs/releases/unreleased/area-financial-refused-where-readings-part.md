Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`XNPV` is `#VALUE!` at a rate of exactly 0, and where its last addition nearly cancels, so that Excel's adjustment of a last addition would give 0, until Excel has been asked; it gave a number there. `PV` is `#VALUE!` at a rate of -1 with a number of periods above 0, where it gave `#NUM!`. A Formula that relied on such arguments shows `#VALUE!`.
