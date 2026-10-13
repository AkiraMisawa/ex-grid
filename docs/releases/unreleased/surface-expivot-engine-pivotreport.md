Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.Engine.PivotReport` is no longer public: a Consumer computes a report through `PivotReportSource.From(source)` and reads it from `WindowAsync`, whose update carries the report's extent, columns and headers in `PivotReportMetadata` and its rows as `PivotDisplayRow`s, and whose metadata asks for a cell's records (`DetailsQuery`), as the Engine's README now shows (ADR-0151).
