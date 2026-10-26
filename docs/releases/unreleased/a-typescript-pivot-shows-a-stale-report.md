Product: ExPivot
Kind: Added
ADR: 0067, 0370

ExPivot drawn by the Script Core (`@ex-grid/pivot`, `@ex-grid/pivot-react`) now shows a Stale Report when newer data cannot be shown: an answer past `maxLeaves`, a refusal or a failure of the source keeps the report on the last data it could show, mutes its numbers, says as of when and why under the Pivot Toolbar, and offers Retry. Copy is refused while it is stale. A new `onFailed` option is raised with each failure of the source, and a failure the notice says is not thrown. A redraw asked for a source's notice no longer marks the pivot busy, as in the Blazor component.
