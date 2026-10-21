Product: ExGrid
Kind: Added
ADR: 0343

A grid built at run time from a shape can fetch its rows from a server a page at a time: `ShapedGrid.Fetch` takes a grid view's spec and a fetch that answers the grid's query with a `ShapedPage`, the rows as a Snapshot. A page whose columns are not the spec's fails its fetch, naming the input, and a date column with no format shows its time there. `FetchingGridSource.Requery` asks again from the top when state of the Consumer's own that the fetch reads has changed, and `ShapedRow.Page` is the Snapshot a row came back in.
