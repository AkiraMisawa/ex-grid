Product: ExBoard
Kind: Added
ADR: 0343, 0344, 0345

`@ex-grid/board` binds a Board over sources held on a server, as `BoundBoard.BindAsync` does. Pass them with `createBoard`'s `sources` option or the `ExBoard` React component's `sources` prop, or bind with `bindBoardThrough`. A grid's rows are fetched a page at a time. A pivot reads the `PivotSource` the source's `pivot(narrowing)` makes. A chart is read through the source's `thin` at `boardChartPixels`, and a link from a pivot asks its `under`. Every question carries the link conditions narrowing the view. `valuesUnder` is the reference a server answers `under` as. A fetched grid whose source fails says so under the grid.
