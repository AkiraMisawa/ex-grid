Product: ExPivot
Kind: Behaviour
Docs: expivot/values#number-formats-and-----
ADR: 0005

A copy with headers carries a number column Item's header in full in the HTML that Excel reads (`123456789012.345` where the header shows `1.23457E+11`), and as shown in the text flavour, as the Item's cells and a number Item's label are copied. A report a server computes carries it in `PivotReportCopyBlock.HeaderRaws`.
