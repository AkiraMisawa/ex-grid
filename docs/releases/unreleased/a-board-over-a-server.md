Product: ExBoard
Kind: Added
ADR: 0343

An input too large to download is bound to a `BoardSource` in the component's `Sources`, or in `BoundBoard.BindAsync`. The Board asks the source for its shape and checks it before anything is drawn. A grid's rows are then fetched a page at a time, and each fetch, like each report source the source makes for a pivot, is handed the conditions the Board's links narrow the view by (`BoardQuery.Narrowing`), to execute where the data lives. A selection reaching rows not fetched narrows nothing and is said under the grid.
