Product: ExPivot
Kind: Added
ADR: 0066

`@ex-grid/pivot-engine` adds `pivotCubeAsync`, `pivotReportAsync` and `PivotReport.hasSameRowsAsAsync`, which make the cube from an answer, lay the report out from a cube and compare two reports' rows in slices of 30 ms by default (PV-40), yielding to the browser between them, and stop at the next slice when their `AbortSignal` is cancelled. They give what the synchronous forms give, and reject with what those throw. It is published with 0.3.0.
