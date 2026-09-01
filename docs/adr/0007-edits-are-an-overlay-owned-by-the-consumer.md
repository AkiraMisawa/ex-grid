# Edits are an Overlay owned by the Consumer. The grid only reports the intent

The grid **can** edit cells but does not **hold** the result. A committed edit goes to the
Consumer as an "intent", and the Consumer records it as a **sparse diff laid over an immutable
base (an Overlay)**. The screen changes when the Consumer returns new row instances.

```
Scenario {
    BaseSnapshotId : "abc123..."        ← a reference; the rows themselves are not held
    Overrides      : { R-4471 → { BreakDate: 2031-06-15 } }   ← only the changed columns, sparsely
    AddedRows      : [ ... ]
}
```

**Being editable is not what makes something a Sheet.** The split in `CONTEXT.md` is **data
ownership and the presence of a formula engine**; editing is not among the criteria. If the grid
only reports the intent and does not own the data, an editable DataGrid is not a contradiction.

## The line is drawn at "committed"

| | Held by | Examples |
|---|---|---|
| **Uncommitted** (transient) | **the grid** | half-typed text in the editor, Selection, Focus, scroll position. Never saved, never shared |
| **Committed** | **the Consumer** | the fact that a row now carries a break date. Saved (as a JSON blob), shared with other screens, sent to the backend |

For the first Consumer, the scenario is persisted as a document in a mutable store and rows are a
concern **shared between two screens**. Something that is saved and shared across screens must not
be hidden inside a display component's internal state.

## The Overlay is applied on the side that builds the Window

**It must be applied every time a Window is built.** A user edits, scrolls the row off screen,
and comes back; the Consumer fetches that range again, and the server — which knows nothing of
the override — returns the plain row. If application is a one-off post-processing step, **the
edit disappears.**

```
✅  applying the Overlay is part of assembling the Window
❌  someone paints the Overlay onto a finished Window afterwards
```

The grid receives a Window and paints it; it knows nothing about the Overlay
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). Only the Consumer knows
both the base and the override, so only the Consumer can compose them. Had the grid been designed
to take a collection of rows, that composition would have been the grid's job.

## Undo and reset

- **Reset needs no implementation.** Because the Overlay is a sparse diff over the base, reset is
  **deleting an entry**. There is not even anything to save first — the original is in the
  immutable base. Per cell, per row or for the whole scenario, it is the same operation. Had the
  design copied rows and mutated the copies, the original values would have had to be kept
  separately just to support reset.
- **The Consumer owns the undo stack.** The grid only forwards Ctrl+Z. The reason is that **two
  stacks break the ordering** — if a user adds a row through a dialog and then edits a cell in
  the grid, a stack owned by the grid does not know about the first, so Ctrl+Z disagrees with
  what actually happened. Only the Consumer sees both.
- **The undo stack implementation is nevertheless bundled with the library.** Ownership and
  driving are the Consumer's, but the Consumer should not have to write it (the same reasoning as
  bundling `GridSource` in ADR-0001). Without it, a component claiming Excel-like operability
  would ship with Ctrl+Z doing nothing by default.
- **Ctrl+Z inside the editor is a different thing.** It undoes uncommitted typing and has nothing
  to do with scenario history. The line above applies unchanged.

## Consequences

- **Repainting after a one-cell edit costs almost nothing.** Only one row's identity changes, so
  row-level memoisation
  ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)) skips the rest. The
  measured 1.90 ms is the figure for all 40 rows.
- **The fact that something was edited is shown through Cell State "modified"**
  ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)). The original value can ride
  along as accompanying data on the tooltip.
- **A two-valued provenance marker stops being enough.** A Consumer that distinguishes "real" from
  "generated" rows now has a third state — **an override on an existing row** — to represent.
- **Sorting and filtering on an overridden column stays explicitly the Consumer's
  responsibility.** The server does not know the Overlay, so sorting there naively puts overridden
  rows in the wrong place. **The grid cannot detect this, but it also does not sort** — since
  [ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) made the interface push,
  ordering was the Consumer's from the start. The Consumer's options are to send the scenario to
  the server so it can order with the Overlay applied, or to put that particular screen on
  `GridSource.From` and order in memory.
- **The code that applies the Overlay must be single, and that single implementation is provided
  by the library.** If the implementation used for display and the one the server uses for
  computation are written separately, **the value on screen and the value used in the computation
  can diverge**. Where an edited field changes a downstream calculation, that is a silent
  numerical error. Details below.

## The three links an edit passes through

For an edited value to appear on screen, three independent conditions must all hold. **Only the
first is automatic; the other two are left to the Consumer's implementation, and breaking either
fails quietly.**

```
1. State changed → the component re-renders                     ★automatic
     (with Fluxor, FluxorComponent calls StateHasChanged)

2. The Overlay has been applied to the contents of that Window  ★manual
     If missed: the grid re-renders but the contents are pre-edit → the screen does not change

3. The changed row is a DIFFERENT INSTANCE                      ★manual
     If missed: the row's ShouldRender sees "same instance" and skips
                → the screen does not change either, and the cause is harder to see than (2)
```

Point 3 is not hypothetical. It is the mirror image of the failure hit in
`spikes/render-bench` when `ShouldRender()` was left to Blazor's automatic detection (a mutable
reference type is treated as "may have changed" even when the reference is identical).

### The worst failure: the colour changes and the value is stale

**Cell State does not travel through the `Window`.** The Consumer answers "this cell is modified"
by consulting the Overrides directly
([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)). So even when 2 or 3 is broken,
**the cell background alone turns to "modified"**.

```
Cell background : shows the "modified" colour  ✓
Cell value      : still the pre-edit value     ✗
Computation     : uses the new value, because the Overrides are correct
```

The user reads this as "my input was applied" while the actual computation runs on a different
value. **The screen and the computed result disagree, and each looks normal on its own.**

### Countermeasures

- **The library provides the Overlay application.** Guarantee in the implementation that new
  instances are returned, so the Consumer never has to remember point 3. This strengthens
  "application is a single implementation" to "that single implementation is the library's".
- **Add a development-time check.** When an edit is committed and **no row identity changes** in
  the renders that follow, warn on the console. That catches both 2 and 3. Disabled in release
  builds, so it costs nothing.

## What wiring the editor settled — and what it deliberately did not

*(Recorded when the first working edit demo was built.)*

**`InMemoryGridSource.ReplaceRow(row, replacement)` is the library-provided apply for the
Consumer that owns its rows in memory** — the `GridSource.From` case this ADR's sorting
paragraph already names. It closes the three links above by construction: it **refuses the
same instance** (point 3, the in-place rewrite, rejected by name rather than warned about),
replaces the row in the base, requeries under the Filter and Sorts in force (point 2 cannot be
missed, because application and requery are one call), and moves the Row Sequence Version
**only when the visible sequence actually moved** — so an ordinary value edit keeps the
selection and continuous entry survives, while an edit that reorders under the current sort
drops it, exactly as
[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md) requires.

**It is not the Overlay application.** A source that owns its rows has no base/override split:
there are no Overrides to consult for Cell State "modified", and no reset-by-deleting-an-entry —
the pre-edit instance is gone from the base. The single library-provided **Overlay**
application this ADR promises — and the bundled undo stack with it — remains to be built, and
its trigger is the first scenario-backed Consumer, where the base is fetched and the diff is
what gets persisted. `ReplaceRow` neither replaces that obligation nor prejudges its shape.
