Product: ExGrid
Kind: Added

`@ex-grid/core` now exports what its Blazor counterpart offers a Consumer beside the grid: `GridMetrics` (with `GridMetricsOptions` and `HeaderOptions`), from which the widths ExGrid measured its own font at are read for a Wrapper's `font`; `textOf` and `displayText`, the text a cell shows; `findStep`, the reference Find step to answer a source's `find` with; and `applyQuery`, the reference filter and sort, for a server that should sort and filter as `GridSource.from` does. `@ex-grid/board` exports `shapedGrid`, C#'s `ShapedGrid.Of`: the source, columns and pinning a grid view's spec describes over a Snapshot. It is published with 0.3.0.
