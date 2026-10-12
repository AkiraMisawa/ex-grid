Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.Engine.PivotCube` is no longer public: it is what the engine holds of a Pivot Source's answer, and a Consumer computes a report through `PivotReportSource.From(source)`, which lays a new layout out from the aggregates it holds whenever the layout asks the same question (what `PivotCube.Holds` told). `PivotSource.AggregateAsync` still answers the Leaf Aggregates, and `PivotJson` still writes them.
