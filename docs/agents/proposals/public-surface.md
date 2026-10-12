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

## ExPivot.Engine, ExPivot and ExPivot.MudBlazor (list expivot)

Nothing for `docs/adr/`, `CONTEXT.md` or `docs/definition-of-done.md`. ADR-0060's note on the whole-report path is
already written, and the Engine's README now computes through `PivotReportSource` as that note says. Two things for
the orchestrator to know:

- **`MudPivotButton` stays public, marked `[EditorBrowsable(Never)]`, where the list said internal.** The list took
  it to be a C# component that can be internal as ExPivot's `PivotGridHost` and `PivotDetailsGrid` are; but those two
  are placed through a `RenderTreeBuilder`, and the Wrapper's five Razor parts place `MudPivotButton` in markup,
  which Razor binds only to a public component (RZ10012 at the first build). So it is one of ADR-0201's "Razor
  components a Consumer does not place", and no Change Note is written for it. Making it internal means placing it
  from code in those five parts, which is the kind of rewrite ADR-0201 rejected for `0.1.0`; the review before `1.0`
  can reconsider it with the rest. No ADR text changes: ADR-0201 already says ExPivot.MudBlazor's parts are public.
- ADR-0201's Context says ExPivot.MudBlazor declares no `InternalsVisibleTo`; that was true when it was written, and
  it now grants `ExPivot.MudBlazor.Tests` (this branch), a test project, for `MudPivotIcons`.
