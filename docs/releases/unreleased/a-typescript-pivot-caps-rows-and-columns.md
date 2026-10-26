Product: ExPivot
Kind: Added
ADR: 0066, 0370

ExPivot drawn by the Script Core takes `maxRows` and `maxColumns`, Excel's 1,048,576 and 16,384 unless given, as the Blazor component's `PivotCaps` does: a layout whose report has more rows or columns is refused by name, and newer data past them leaves a Stale Report that names the cap.
