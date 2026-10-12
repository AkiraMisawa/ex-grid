Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.Engine.PivotReportRow` is no longer public: the row a Consumer reads is `PivotDisplayRow`, from a report source's Window, with the same role, key, Value Field, labels and `CarriesValues`, and its values and Item path besides.
