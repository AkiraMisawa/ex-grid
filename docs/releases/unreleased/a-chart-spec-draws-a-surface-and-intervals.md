Product: ExChart
Kind: Added
ADR: 0320, 0274, 0303, 0304

A Chart Spec draws a 3D surface: a `surface` in place of `axis` and `series` names a table's `x`, `y` and `z` columns on a grid, or a mesh's `key` and `triangles`, with its colours' `centre`, its scales and the `method` a capability interpolated with. `ChartSpecReading.Surface` holds it for `ExSurface`, and its `Definition` throws for a surface spec, which has no chart of series. A series takes `interval` and an observation `xInterval`, each a `lower` and an `upper` column with its `meaning`. A Board refuses a surface spec as a view, by its path.
