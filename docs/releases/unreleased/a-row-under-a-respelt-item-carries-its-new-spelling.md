Product: ExPivot
Kind: Fixed
ADR: 0153

When a Change Batch took away the record that gave an Item its spelling, a row beneath that Item kept the old spelling in its key's Items (`PivotReportRow.Key.Items`, and a Window row's `items`), though its labels and its row path took the new one. The row now carries the spelling a fresh report gives, in both engines.
