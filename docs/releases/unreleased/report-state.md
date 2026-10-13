Product: ExPivot
Kind: Breaking
ADR: 0151

`ExPivot.Report` is now a `PivotReportState`, the report's metadata with the Window on screen and its rows, where it was the whole `PivotReport`. `PivotReportRow.Report` and `PivotReportRow.ValueAt(column)` are gone, and since ADR-0201 so is the whole `PivotReport`: read a row's values from the `PivotDisplayRow`s in its `Rows`, with `ValueAt(column)` (#70).
