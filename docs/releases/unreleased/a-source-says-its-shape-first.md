Product: Data
Kind: Added
ADR: 0341

A source held on a server says its shape, its columns' names and kinds, before any row. `FetchingGridSource.Shape`, `FetchingPivotReportSource.Shape` and `SnapshotArrow.ReadShapeAsync`, which reads an Arrow stream's schema alone, answer it through `IShapedSource.ShapeAsync`; a pivot source over a Snapshot answers the Snapshot's own. `BoardInput.CheckAsync` checks an input against it and refuses a source that says none.
