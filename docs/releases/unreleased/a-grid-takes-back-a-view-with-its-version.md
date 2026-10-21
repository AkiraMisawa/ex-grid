Product: ExGrid
Kind: Added
ADR: 0331

`ExGrid.ReadView()` gives the Selection, with the Row Sequence Version it is written in, and the scroll offsets as a `GridView`; `ShowViewWith(view)` hands one back ahead of the version it is written in, and the grid takes both in the render that takes that version, in place of the empty Selection a new version leaves. A Consumer that shows one set of rows after another in one grid keeps each set's view with them. Handed from the handler of one of the grid's inputs, the inputs after it in the Input Order are taken under the new version, though on Blazor Server they told the browser's paint before it.
