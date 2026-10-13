Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.SourceCell` is no longer public, and neither is `PastePlan.SourceCellFor`, which returned it. `GridPasteIntent.ValueFor` and `OriginFor` answer what a target cell receives.
