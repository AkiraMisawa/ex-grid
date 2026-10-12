Product: ExPivot
Kind: Behaviour
Docs: expivot/layout#report-forms--subtotals-and-grand-totals
ADR: 0130

An error value among the selected cells of a report (`#DIV/0!`, `#NUM!`) now leaves Count and Numerical Count, as Excel's status bar does, whether the report is laid out in the browser or answered by a server; Average, Min, Max and Sum are still shown nowhere.
