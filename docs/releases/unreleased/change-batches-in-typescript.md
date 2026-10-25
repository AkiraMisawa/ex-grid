Product: Data
Kind: Added
ADR: 0064, 0067

`@ex-grid/data` applies Change Batches: `createChangeBatch` takes the records added, the records changed and the Record Keys removed, and `applyChangeBatch` makes the next Snapshot from them whole or not at all, saying which rows left and which came, as `Snapshot.Apply` does. A refusal names the row, column and key in the same words as `SnapshotException`; both packages run the same cases under `tests/cases/data/`. It is published with 0.3.0.
