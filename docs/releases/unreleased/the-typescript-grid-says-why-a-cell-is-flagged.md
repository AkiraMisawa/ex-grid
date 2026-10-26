Product: ExGrid
Kind: Added
ADR: 0034

`@ex-grid/core`'s grid takes `cellMessage`, asked for one cell once the Focus has stood on it for 300 ms or the pointer has come to rest over it, never while painting, and shows the answer beside the cell until the Focus moves or the pointer leaves the grid; an open editor's own message outranks it. `GridRuleset`, the bundled ruleset the Blazor grid already ships, holds column rules (a column's `validate` is `validatorFor(name)`) and row rules, and after each `evaluate` over the rows the Consumer holds it answers both `cellState` and `cellMessage`, so a paste and a change from program code wear the same marks as a typed edit. A Selection scrolled wholly out of view is now said below the grid with its size, "{count} cells selected (outside the visible range)", with a Go to selection button that brings its Focus back, both worded through `commandLabel` (`selectionLabelIds`).
