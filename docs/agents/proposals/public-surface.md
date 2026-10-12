# ADR-0201's public surface: edits for files a background agent does not edit

Branch `claude/public-surface`, 2026-10-12. Each section is one agent's, for the orchestrator to apply.

## ExGrid, the core (lists exgrid-cells, exgrid-components, exgrid-data-shape)

One edit to `docs/adr/0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md`. The grid
has 25 `[JSInvokable]` methods, at the lists' base (ef0bd9e2) and at this branch's tip: the twenty-sixth textual
match is a comment in `ExGrid.razor` that says why an armed flag is not left standing. Each now carries
`[EditorBrowsable(Never)]`.

- Old (Context, the last bullet): ``The grid's 26 `[JSInvokable]` methods are public because``
  New: ``The grid's 25 `[JSInvokable]` methods are public because``
- Old (The decision, "Rejected for `0.1.0`"): `moving the 26 interop methods to an internal receiver object`
  New: `moving the 25 interop methods to an internal receiver object`

Nothing for `CONTEXT.md` or `docs/definition-of-done.md`. The criteria that name a type made internal as the place a
layer-1 test reads (`ColumnGeometry`, `ViewportGeometry`, `ViewportBox`, `OverflowRules`, `GridKeys`,
`CellClasses` / `RowClasses`) stay true: the tests reach them through `InternalsVisibleTo`.

## ExSheet, ExSheet.Engine and ExSheet.MudBlazor (list exsheet)

Nothing for `docs/adr/`, `CONTEXT.md` or `docs/definition-of-done.md`. ADR-0100's note on `ToolbarOrder` is already
written, and no ADR, criterion or glossary entry names another type or member made internal here. ADR-0201's Context
says ExSheet.Engine declares no `InternalsVisibleTo`; that was true when it was written (2026-10-11), and the engine
now grants `ExSheet.Engine.Tests` (since #83) and `ExSheet.Components.Tests` (this branch), both test projects.
