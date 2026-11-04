Product: ExGrid
Kind: Added
Docs: exgrid/sorting-filtering#find-all
ADR: 0470

The find panel gains Find All, Excel's list of every match with its exact count, where the source answers it:
`GridSource.From` does, `GridSource.Fetch` takes a `findAll` delegate, and an `IGridSource` answers `CanFindAll` and
`FindAllAsync` (in React, `findAll` beside `find`). A line chosen moves the Focus there.
