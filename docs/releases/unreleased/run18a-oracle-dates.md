Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

Formulas that were refused now answer as Excel does, as Windows run 18a read it: `EDATE` from serial 0 counts day 0 as `DATE` does (`EDATE(0,0)` is 0, `EDATE(0,2)` 60 and `EDATE(0,97200)` `#NUM!`); a boolean typed into `EDATE`, `EOMONTH`, `NETWORKDAYS` or `WORKDAY` is `#VALUE!`, which a Formula catches; `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for a serial between -1 and 0; `MOD` refuses nothing, giving `MOD(-1E-17,1)` 0 and `MOD(-0.084474,2270)` 2269.915526; and `INDIRECT`'s R1C1 text joining a cell to a whole column, `R2C2:C3`, or a whole row to a cell, `R3:R2C2`, names the rectangle the parts span, where it was `#REF!`. A whole column joined to a cell, `C3:R2C2`, is `#VALUE!` until Excel is asked, where it was `#REF!`.
