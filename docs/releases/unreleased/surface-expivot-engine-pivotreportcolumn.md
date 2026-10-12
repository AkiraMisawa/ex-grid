Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.Engine.PivotReportColumn` is no longer public: the value column a Consumer reads is `PivotDisplayColumn`, in `PivotReportMetadata.ValueColumns`, with the same name, header, role and Value Field, and its Item path besides.
