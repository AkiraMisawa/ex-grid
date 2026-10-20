Product: Data
Kind: Added
ADR: 0341

`BoardInput` names a piece of data and declares each column's kind; `Check` and `Bind` hold a source's `DataShape` against it and refuse a missing column, or one of another kind, naming the input and the column. `FromJson` makes a Snapshot from JSON rows by the declared kinds, and refuses a value that does not convert by its row and column rather than drop it or read it as zero. Refusals are a `BoardRefusedException` carrying the path.
