Product: ExGrid
Kind: Added
ADR: 0331

`@ex-grid/core`'s grid handle gains `readView()` and `showViewWith(view, rowSequenceVersion)`, as the Blazor component's `ReadView` and `ShowViewWith` do: a Consumer that shows several sets of rows in one grid, one at a time, keeps each set's Selection and scroll offsets and hands them back with the rows, and the grid shows them in place of the empty Selection a new version leaves. A Selection that no longer fits the rows is dropped.
