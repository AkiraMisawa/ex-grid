Product: ExPivot
Kind: Added
ADR: 0066, 0067

`@ex-grid/pivot-engine`'s `SnapshotPivotSource` takes Change Batches: `apply` folds a batch into the answer held for the last question, and `subscribe` tells each listener the new Source Version. Its questions, Items and records are read in slices of 30 ms by default (PV-40), and a question whose `AbortSignal` is cancelled stops at the next slice. After a batch it reads the records in the Snapshot's order, so a sum of doubles may differ in its last bit from the C# source's, which reads them in its storage's order. It is published with 0.3.0.
