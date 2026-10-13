Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.ColumnGeometry` is no longer public, and neither are `ColumnStyles`' constructor and `Geometry`, which named it: it is the grid's own horizontal slicing (ADR-0004, ADR-0013). Nothing replaces it; a Consumer declares widths on its columns and hears a resize as a `ColumnWidthChange`.
