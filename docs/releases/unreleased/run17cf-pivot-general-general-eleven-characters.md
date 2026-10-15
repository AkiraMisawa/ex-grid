Product: ExPivot
Kind: Behaviour
Docs: expivot/values#number-formats-and-----
ADR: 0060

A value whose Value Field has no number format is shown in Excel's General as a PivotTable shows it, at most eleven characters besides a minus sign at any width, where up to 15 significant digits were shown: √2 reads `1.414213562`, a sum with twelve or more integer digits `1.23457E+11`, and a negative value takes `-` in every culture, where sv-SE, fi-FI and nb-NO wrote `−` (U+2212). A copy still carries the full-precision number in the HTML that Excel reads, and the text flavour carries what the cell shows; a Consumer that wants more digits gives the Value Field a `NumberFormat`.
