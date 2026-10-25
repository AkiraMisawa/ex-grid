Product: ExPivot
Kind: Added
ADR: 0370

`@ex-grid/pivot-engine` has `PivotJson`, the C# engine's `PivotJson` in TypeScript: it writes and reads every question and answer a `FetchingPivotSource` sends a server (the Leaf Aggregates, pages of Items and of records, the counts of Items, a notice that the data moved on), the same document to the byte, so a browser can ask a server built on `ExPivot.Engine`. `PivotSourceChanged.sourceVersion` is optional, as a server's notice may not know the new version yet, and `pivotDetailsQuery` without a `count` asks for 2^31 − 1 records, C#'s `int.MaxValue`, so that the question crosses to a C# server.
