Product: ExGrid
Kind: Added
ADR: 0050

`@ex-grid/core` takes `dataEdge`, as the Blazor component's `DataEdge` parameter does: handed the Focus and a direction, the Consumer answers where Ctrl+arrow and Ctrl+Shift+arrow stop, and an answer off the Focus's line is refused by name. Home and End keep the grid's edge. ExSheet in TypeScript answers it from the Sheet's filled cells, as Excel does.
