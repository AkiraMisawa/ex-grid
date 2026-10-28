Product: ExPivot
Kind: Added
ADR: 0410, 0240

`PivotHandle` in `@ex-grid/pivot` gains `gridAround(element)` and `holdSettled(element)`: the grid that takes the inputs made at a part, the report's or a Show Details grid's, and a hold of its Settled Mark that waits for that grid where it does not stand yet. They are for a Framework Adapter whose Chrome's contents arrive later than their slot, as Blazor's do; a stand-in `PivotHandle` of your own adds both. It is published with 0.3.0.
