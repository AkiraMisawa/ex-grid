Product: ExPivot
Kind: Breaking
ADR: 0201

These members of `PivotEngine` are no longer public, each naming the engine's whole report or its cube: `Compute`, `Aggregate`, `Cube`, `CubeAsync`, `Report`, `ReportAsync`, `CanReuse`, `ItemsOf(PivotCube, PivotLayout, string, PivotOptions?)` and `RecordsBehind`. A Consumer computes a report with `PivotReportSource.From(source).WindowAsync`, over records in memory through `PivotSource.From(records, fields)`; lists a field's Items with the report source's `ItemsAsync`, or orders a page of them with `PivotEngine.ItemsOf(PivotItemPage, …)`, which stays; and asks for a cell's records with `PivotReportMetadata.DetailsQuery` and `DetailsAsync`.
