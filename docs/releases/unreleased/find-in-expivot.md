Product: ExPivot
Kind: Added
ADR: 0332

Ctrl+F in the report opens the report grid's find panel and finds a row label, a value, a subtotal or a grand total by its displayed text, the rows beyond the ones on screen included; a collapsed group is not searched inside, and nothing is expanded to show a match. The matches on screen are marked as they are in ExGrid (`--ex-match-mark-fill`, `--ex-match-mark-current-fill`). Nothing is wired for it; over a server's report (`PivotReportSource.Fetch`) the rows beyond the Window are read through its `copy` transport, by the Report Version on screen.
