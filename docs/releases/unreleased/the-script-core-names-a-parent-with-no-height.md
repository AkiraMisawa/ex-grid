Product: ExGrid
Kind: Added
ADR: 0028

The TypeScript core hears a `'stretch'` height whose parent gives it none through `onParentWithoutHeight`, once until a real height arrives, and Blazor's `ExGrid.Components.ExGrid` logs its warning from it ("ViewportHeight is Stretch, but the grid's parent gives it no height…").
