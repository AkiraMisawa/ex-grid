Product: ExGrid
Kind: Added
ADR: 0342

`ShapedGrid` builds a grid's columns at run time from a source's shape and a grid view's JSON spec (`GridViewSpec`), with no row type to write: hand its `Columns`, `Source` and `PinnedColumnCount` to an `ExGrid` of `TRow="ShapedRow"`. A decimal or double column must carry an Excel format code such as `#,##0.00`, and one left out is refused naming the column; integers and dates have fixed defaults. Each row is its own Row Key, so handing some of `Rows` back to `Source.ReplaceAll` narrows the grid and keeps the sort and filter the user set.
