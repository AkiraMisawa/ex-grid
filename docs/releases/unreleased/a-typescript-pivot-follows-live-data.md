Product: ExPivot
Kind: Added
ADR: 0067, 0370

ExPivot drawn by the Script Core (`@ex-grid/pivot`, `@ex-grid/pivot-react`) now follows a live source: a `SnapshotPivotSource` a Change Batch is applied to, or a `FetchingPivotSource` whose transport tells of new data, redraws the report on screen without a Refresh. Notices are gathered and asked for at most once per `redrawInterval`, 250 ms unless given, as the Blazor component's `RedrawInterval`.
