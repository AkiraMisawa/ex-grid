Product: ExGrid
Kind: Added
Docs: exgrid/columns#keeping-a-view
ADR: 0207

`GridViewStateJson` writes a user's View State — the columns in order with their widths and pins, the sort and the filter, gathered by `GridViewState.Of` — or a `GridQuery` alone as versioned JSON, and reads it back with each filter operand as the type it was and an Auto width as the intent. A stored view that cannot be read, such as one naming a column the grid no longer has, is refused with a `JsonException` that names the part.
