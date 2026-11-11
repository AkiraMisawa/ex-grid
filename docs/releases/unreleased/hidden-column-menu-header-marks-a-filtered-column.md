Product: ExGrid
Kind: Behaviour
Docs: exgrid/columns#a-compact-header
ADR: 0502

With `HideColumnMenu` (`hideColumnMenu` in ExGrid for React) a filtered column's header now shows the funnel too. The default
header, which has no ▾ then and keeps no room for one, takes the ▾'s band for the funnel while the `Filter` holds a spec for
its column: a mark named `Filtered` for a screen reader, with nothing to press. A grid that hides the column menu and is
given a filter will look different on its filtered columns, and an Auto column whose header decides its width is 22 px
wider for as long as it is filtered; a width that was declared, dragged, fitted or restored is not changed, and no width is
reported. The compact header already marked such a column in its box; its one header without a box, a column that cannot
be sorted, takes the box the same way. `GridMetrics.HeaderRequiredPx` has an overload that takes `filtered`
(`headerRequiredPx` and `gridHeaderRequiredPx` take it in `@ex-grid/core`), and charges it only for these headers.
