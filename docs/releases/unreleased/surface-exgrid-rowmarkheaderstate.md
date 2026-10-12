Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Rows.RowMarkHeaderState` is no longer public, and neither are `RowMarkRules.HeaderState` and `HeaderPress`, which named it: the header checkbox is the grid's to paint and to answer (ADR-0043). `RowMarkRules.LineUp`, which a Consumer answering `OnMarkIntentAsync` brings its rows into line with, stays public.
