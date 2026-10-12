Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

At a rate other than 0, `PMT`, `PV` and `FV` with a number of periods that is not a whole number, or is negative, are now `#VALUE!` where they gave a number, until Excel has been asked how it raises 1+rate to one; at a rate of 0 every number of periods is still answered. A Formula that relied on a fractional `nper` shows `#VALUE!`.
