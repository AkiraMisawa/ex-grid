Product: ExGrid
Kind: Fixed
ADR: 0342

`ShapedGrid.From`'s source now names each row by its `Position` in the Snapshot, not by the row object, so a shaped grid draws on Blazor's `ExGrid.Script.ExGrid`, which refused a Row Key that is not text, a whole number or a Guid.
