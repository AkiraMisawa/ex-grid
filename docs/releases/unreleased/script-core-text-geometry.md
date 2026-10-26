Product: ExGrid
Kind: Added
ADR: 0350

`@ex-grid/core` exports `gridTextGeometry`, the widths a grid at a given density charges each character class, its cells' padding and single glyphs, as plain data a Worker is handed as it is, and `gridHeaderRequiredPx`, the width a header needs as an Auto width charges it. A Consumer that fixes a column's width beside the grid's own, such as ExPivot's label columns, charges text alike. They are published with 0.3.0.
