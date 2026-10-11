Product: ExPivot
Kind: Breaking
ADR: 0151

`ExPivot.Report` is now a `PivotReportState`, the report's metadata with the Window on screen and its rows, where it was the whole `PivotReport`. `PivotReportRow.Report` and `PivotReportRow.ValueAt(column)` are gone: read a row's value with `PivotReport.ValueAt(row, column)` (#70).
