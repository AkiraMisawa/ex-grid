Product: Data
Kind: Added
ADR: 0130

`ExGrid.Data` holds the Aggregations' definitions, `Aggregation` and `AggregateArithmetic`, which ExPivot's Value Fields and ExGrid's Selection Summary both read, so a pivot cell and the status bar cannot disagree over the same values. `ExGrid` now depends on `ExGrid.Data`, at exactly its own version (#65).
