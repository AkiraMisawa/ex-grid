Product: ExPivot
Kind: Added
ADR: 0370

`@ex-grid/pivot-engine`, a new npm package that depends only on `@ex-grid/data`, is ExPivot's engine in TypeScript: the Snapshot, record and fetching Pivot Sources, the Leaf Aggregate pass with decimals summed exactly where `ExPivot.Engine` sums them, the Field List and Layout menu edits, the layout's JSON, and the report in the Compact, Outline and Tabular forms under every locale and set of words. Both engines run the same cases under `tests/cases/pivot/`, and a differential run holds whole reports over random data to the C# engine. It is published with 0.3.0.
