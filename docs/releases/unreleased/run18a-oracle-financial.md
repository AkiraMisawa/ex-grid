Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

Formulas that were refused now answer as Excel does, as Windows run 18a read it: `PMT` is `#NUM!` at a rate of -1 or below and with no periods at the start of each period, and gives the exact payment rounded once where a number it works with passes a double's range, as Excel's `=PMT(1,1020,1000)`, -1000, and `=PMT(-0.5,360,1000,500)`, -250, do; `PV` at a rate of -1 with periods is `#DIV/0!`; and `XNPV` is `#NUM!` at a rate of 0, sums its values plainly where the last addition nearly cancels, and gives the Error Value Excel gives where its checks are answered in an order Excel's answers settle. Each was `#VALUE!`. Where Excel's answer needs another evaluation of `PMT` than the one ExSheet keeps, as at `=PMT(0.1,2,1000,-1210)`, `PMT` stays `#VALUE!`.
