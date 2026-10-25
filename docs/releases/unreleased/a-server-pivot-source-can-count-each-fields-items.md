Product: ExPivot
Kind: Added
ADR: 0370

A Pivot Source may now declare that it counts each field's Items (`PivotSourceFeatures(…, countsItems: true)`) and answer them with `ItemCountsAsync`, a `PivotItemCounts` under the Source Version it counted, which `PivotSource.Fetch` asks through a fourth delegate after the other three and `PivotJson` writes and reads. `PivotLeafBound.Of(layout, counts)` turns the counts into the most leaves a layout can make, the hint for marking a drop past the leaf cap before it is asked (ADR-0370 section 3); `@ex-grid/pivot-engine` has the same as `itemCounts` and `pivotLeafBound`. The bundled sources answer the counts without declaring them, and ExPivot's Field List does not ask for them yet.
