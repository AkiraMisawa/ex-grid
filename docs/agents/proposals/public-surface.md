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

## The baseline, the package validation and the API reference

Three edits for files a background agent does not edit, and one collision to put to the user.

- **ADR-0201, "The baseline": the first build answered the condition it left open.** The analyzer sees what Razor
  generates: a `[Parameter]` written in a component's `@code` is reported at its line in the `.razor` file, and a
  probe parameter added to `SummaryFiguresMenu` failed the build with RS0016. So no snapshot test replaces it. A
  sentence to append to the section's last bullet: *(2026-10-12: the first build showed it does. A parameter written
  in `@code` is reported at its line in the `.razor` file, so the analyzer is the baseline for every shipped project
  and no snapshot test was written.)* Two facts the section may also want: the analyzer is
  `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.3.0, added once in `src/Directory.Build.props`; and its other rules
  are off there — RS0026 and RS0027 would reshape members with optional parameters, which `0.1.0` does not review
  ("Types are reviewed; members are not reshaped"), and RS0041 reports the `BuildRenderTree` Razor generates with
  nullable annotations off.
- **ADR-0110, "The frame and the reference pages": the API reference is built.** Its bullet *"The API reference is not
  built yet. A page per public type would publish a surface nobody has reviewed, ... Until then each topic page keeps
  its API table."* is now false. Proposed text: *"**API reference** (`/reference/api`, added 2026-10-12 after
  [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md)): every public type
  of the packages, by product in ADR-0042's order, read by the build from the shipped assemblies, each with its
  summary; a type's name opens its API table, the same table a topic page places. What is public and not the
  contract — the types marked `[EditorBrowsable(Never)]`, and the members of contract types the grid's script calls or
  that are so marked — is listed apart, under "Not the contract: may change in any version", and every API table lists
  such members apart the same way. The topic pages keep their API tables."*
- **ADR-0190, "The cut": the step ADR-0201 names is not in its list.** ADR-0201 says *"The cut before a tag moves
  Unshipped into Shipped (ADR-0190)"*, and ADR-0190's numbered list has no such step. Proposed sixth step: *"6. moves
  every line of each `src/*/PublicAPI.Unshipped.txt` but its `#nullable enable` into that project's
  `PublicAPI.Shipped.txt` (ADR-0201)."* One question with it, for the user: whether a prerelease's cut moves them too.
  `PublicAPI.Shipped.txt` is empty today because nothing has shipped as stable; if betas and release candidates move
  them, a member removed after a beta needs a `*REMOVED*` line in Unshipped until the next cut, which is the analyzer's
  way of saying the break, and also its cost.
- **A collision between ADR-0201's cut and ADR-0205's check, for the user.** The Definition of Done's Step 7 clause
  (restated with ADR-0205) has the release workflow refuse an unsuffixed tag unless *"the tag differs from that commit
  [the one `results.md` was made against] only under `verification/` and `docs/releases/`"*. A cut that moves
  `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` changes ten files under `src/`, so the `0.1.0` tag as ADR-0201
  describes it would be refused. Either the move is made before the sign-off run, in the release branch's change that
  freezes the surface (so the results are made against a commit whose Shipped files already hold `0.1.0`), or the
  check also admits `src/*/PublicAPI.Shipped.txt` and `src/*/PublicAPI.Unshipped.txt`. The first keeps the check's
  promise that the signed-off commit is the tagged code byte for byte; the second keeps ADR-0201's wording.

Nothing for `CONTEXT.md`. `AGENTS.md` gained one Toolchain bullet (how a new public member is declared), which a
background agent may edit.
