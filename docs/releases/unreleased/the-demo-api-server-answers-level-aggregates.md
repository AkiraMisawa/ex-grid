Product: ExPivot
Kind: Added
ADR: 0370

The demo API server (`samples/ExGrid.DemoApi`) declares `answersLevels` in `GET /api/pivot/fields` and answers Level Aggregates over SQLite: past `MaxLeaves`, a question with levels gets the cells of the groups it shows, one `GROUP BY` a pair of depths joined by `UNION ALL`, and a set ordered by a value past the window's limit is answered as a Sibling Window with its rest cells. Its answers are `PivotSource.From`'s over the same trades, cell for cell, but for which of two tied siblings beyond a window is the margin.
