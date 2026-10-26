Product: ExPivot
Kind: Added
ADR: 0370

A question asked with `PivotQuery.ForLevels(layout, maxLeaves)` carries `Levels`, the groups the layout shows, and a source that declares `answersLevels` in its `PivotSourceFeatures` may answer it, past `MaxLeaves`, with the cells of those groups instead of a refusal: `PivotAnswerBuilder.AddCell` for each, as `GROUPING SETS` makes them, and a double sum given with the sum of its magnitudes. An answer of cells is refused by name when it names a group the levels do not show, two cells for one group, or a cell without its parent, and `PivotJson` carries it; the bundled sources answer it too, and `@ex-grid/pivot-engine` has the same as `levelQueryForLayout`, `addCell` and `setLevelSum`. `ExPivot.Engine`'s cube refuses it by name; the TypeScript engine lays a report out from it, checking that every set of children adds up to its parent.
