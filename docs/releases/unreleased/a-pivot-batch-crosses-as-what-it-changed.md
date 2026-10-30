Product: ExPivot
Kind: Added
ADR: 0370

`SnapshotPivotSource` raises `Batched` as it applies a Change Batch, just before `Changed`: the records the batch removed and added, as a `PivotServerChangeBatch` a server can write with `PivotJson` for a TypeScript `FetchingPivotSource`. Blazor's ExPivot now hands the browser that batch, which it folds into the answer it holds, where it asked the whole question again after every batch; a source with an Order Key is still told by its notice alone.
