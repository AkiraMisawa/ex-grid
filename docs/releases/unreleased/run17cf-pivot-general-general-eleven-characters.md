Product: ExPivot
Kind: Behaviour
Docs: expivot/values#number-formats-and-----
ADR: 0060

A value whose Value Field has no number format is shown in Excel's General as a PivotTable shows it, at most eleven characters besides a minus sign at any width: √2 reads `1.414213562` and a sum with twelve or more integer digits `1.23457E+11`, where up to 15 significant digits were shown. A copy still carries the full-precision number in the HTML that Excel reads, and the text flavour carries what the cell shows; a Consumer that wants more digits gives the Value Field a `NumberFormat`.
