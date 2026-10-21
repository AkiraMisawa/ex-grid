Product: ExBoard
Kind: Added
ADR: 0344, 0345

A chart over an input held on a server is drawn: the Board asks the source's `BoardSource.Thin` for 2,048 thinned pixels, again when a link narrows the chart or its range is narrowed. A link from a pivot over such an input asks `BoardSource.Under` for the values under the selected cells (`BoardSource.ValuesUnder` is the reference), through `BoundBoard.SelectCellsAsync`. Both were refused before; a source without `Thin` or `Under` is still refused at binding, by path. `BoundBoard.SelectCells` throws for a pivot over a server.
