Product: ExPivot
Kind: Added
ADR: 0340

`ItemsOf` reads what each cell of a selection raised by `SelectionChanged` stands for, as a `PivotCellItems`: the Items its row's path names and, for a value cell, its column's. It answers null when the selection reaches rows the report has not drawn, rather than the part it could read.
