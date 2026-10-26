Product: ExPivot
Kind: Added
ADR: 0370

A server's live data crosses as `PivotJson`: `ExPivot.Engine` writes a Change Batch with `PivotJson.Write(PivotServerChangeBatch)` (and reads it back with `ReadServerChangeBatch`), and `@ex-grid/pivot-engine` writes one with `PivotJson.writeChangeBatch` and reads either notice with `PivotJson.readServerNotice(json, fields)`, to hand to a `FetchingPivotSource`'s `notify`. Both engines write the same text to the byte. A batch names its fields, and a page whose fields differ refuses it by name.
