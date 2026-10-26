Product: ExGrid
Kind: Added
ADR: 0006

`@ex-grid/core`'s `GridColumn` takes `tone`, the column's tone rule that the Blazor `GridColumn` has had: a function of a value that answers whether it is a gain, a loss or neither (`CellTone`). The theme paints the answer through `--ex-tone-positive-color` and `--ex-tone-negative-color`, and the bare grid paints none. A null or missing value is not asked about, and a Cell State still outranks the tone.
