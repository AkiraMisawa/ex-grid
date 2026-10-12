Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Selection.CycleOrder` is no longer public, and neither is `GridSelection.CycleFocus`, which took it: the order Enter and Tab walk the Focus in is the grid's (ADR-0012). Nothing replaces it.
