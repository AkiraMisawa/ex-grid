# Proposals from claude/dod-gaps-grid-tests

## UX-5 and LV-22: a Consumer render that changes nothing no longer renders the grid

**Decided with the user on 2026-10-12:**
- Take the skip, so that UX-5 holds as written.
- Check LV-22 at ADR-0160's own bound.

**Implemented on this branch:**
- `ExGrid.SetParametersAsync` returns without rendering when every parameter it is handed is one it
  already holds: the same instance for a reference type, an equal value for a value type. For a
  pushed Window, it also needs the same count, and the same row instances where the grid last painted
  them, so a row replaced in the same Window array still repaints. That compares only the painted
  rows (ADR-0141's cost rule) and holds only rows of the Window last given (ADR-0160). Every other
  list (Columns, HeaderGroups, sorts) is compared by instance, as ADR-0003 has it. An empty push
  still renders.
- The skip also waits while the grid owes work that only its next render's `OnAfterRenderAsync`
  tells: the editing mode and Point state to the key gate and the Consumer, the key claims, a focus,
  or a sentence. The layer-3 run on the Server host found a Sheet edit whose gate was never told
  its mode, because the skipped Consumer render had been the render that told it (ED-22). The test is
  `VisualTokenAncestorTests.UX5_a_visual_token_set_on_the_wrapping_element_renders_nothing_in_dotnet`.
- LV-22's ExPivot test now checks the bound after the pivot renders again: no earlier Window's row
  is alive but the rows the grid painted from the Window before the newest. Its Viewport is shorter
  than the report, so a holding of a whole Window can be told apart from that bound.

These edits to the ADR and the Definition of Done follow. The option of restating UX-5 is moot and
has been removed.

### 1. ADR-0160, under the ExPivot sub-bullet of "Blazor's renderer keeps the frames …"

old ->
```
    rows reach no report (ADR-0153). *(Found while building LV-22's ExPivot test, 2026-10-08.)*
```
new ->
```
    rows reach no report (ADR-0153). *(Found while building LV-22's ExPivot test, 2026-10-08.)*
  - *(2026-10-12, decided with the user: a Consumer render that changes no parameter no longer renders
    the grid — UX-5, ADR-0027 P3 — so the release of the previous render's painted rows waits for the
    grid's next real render, a new Window or anything else it is handed. The bound stays one render's
    painted rows: at any moment nothing of a Window older than the one the grid's last render replaced
    is held, and of that one only the rows the grid painted from it.)*
```

### 2. ADR-0160, "How it is checked", the ExPivot sentence

old ->
```
  Once the pivot renders again, the grid holds no display row of an earlier Window. *(Restated on
```
new ->
```
  Once the pivot renders again, nothing of an earlier Window is alive but the rows the grid painted from the Window before the newest, until the grid's next real render (2026-10-12, below the bullet on Blazor's frames). *(Restated on
```

### 3. Definition of Done, LV-22 (whole row)

old ->
```
| **LV-22** | MUST | The grid holds no Consumer row instance, nor its Row Key, beyond the Window it was last given, apart from the Window last measured for Auto widths while the measure is deferred, the row of an Action press in flight and the Row Key of the row under an open editor — and the rows the render before the newest painted, which Blazor's renderer keeps as its previous frames: after a run of new Windows and a full collection, weak references to every earlier Window's rows are dead. Under ExPivot, after a run of live redraws, no engine report is alive but those its report source keeps by design: the `versionsKept` newest (two by default), and the reports the Change Highlight compares (each Source Version published within `ChangeHighlightDuration` of the newest, and the one before them). No display row is alive but those of the Window on screen and of the Window last served under the Report Version before it, the Baseline Window of the next Window Changes. Once the pivot renders again, the grid holds no display row of an earlier Window (ADR-0160/0153/0152; restated 2026-10-08, when its test was built, and decided with the user on 2026-10-09) | Layer 2: weak references, with `CellAppearance` declared and not, a Row Key declared and not; ExPivot on `/pivot-live`'s generator, redraws a second and 250 ms apart, local and with its Windows crossing JSON | none alive but the named exceptions; under ExPivot the exact set kept |
```
new ->
```
| **LV-22** | MUST | The grid holds no Consumer row instance, nor its Row Key, beyond the Window it was last given, apart from the Window last measured for Auto widths while the measure is deferred, the row of an Action press in flight and the Row Key of the row under an open editor — and the rows the render before the newest painted, which Blazor's renderer keeps as its previous frames: after a run of new Windows and a full collection, weak references to every earlier Window's rows are dead. Under ExPivot, after a run of live redraws, no engine report is alive but those its report source keeps by design: the `versionsKept` newest (two by default), and the reports the Change Highlight compares (each Source Version published within `ChangeHighlightDuration` of the newest, and the one before them). No display row is alive but those of the Window on screen and of the Window last served under the Report Version before it, the Baseline Window of the next Window Changes. Once the pivot renders again, nothing of an earlier Window is alive but the rows the grid painted from the Window before the newest, which Blazor's renderer keeps until the grid's next real render: a Consumer render that changes no parameter no longer renders the grid (UX-5), and the bound stays one render's painted rows (ADR-0160/0153/0152; restated 2026-10-08, when its test was built, decided with the user on 2026-10-09, and restated with the user on 2026-10-12) | Layer 2: weak references, with `CellAppearance` declared and not, a Row Key declared and not; ExPivot on `/pivot-live`'s generator, redraws a second and 250 ms apart, local and with its Windows crossing JSON | none alive but the named exceptions; under ExPivot the exact set kept, and after the pivot renders again no earlier Window's row but those the grid painted from the Window before the newest, with a Viewport shorter than the report |
```

### 4. Definition of Done, UX-5

No edit. The row names no moment of release, and it holds as written.

### 5. Docs Site (pages are off limits to this branch): `samples/ExGrid.Docs/Pages/Grid/GridOverview.razor`, "Two rules that keep it fast"

The page is still true. It could now say what the rule earns.

old ->
```
                any delegates you pass in fields. A new array or lambda on every render looks like a change and renders
                every row again.
```
new ->
```
                any delegates you pass in fields. A new array or lambda on every render looks like a change and renders
                every row again; handed nothing new, the grid does not render at all, so restyling the element around it
                costs no render.
```

## Lists changed in place: Columns, HeaderGroups and Sorts are taken as new lists (decided with the user on 2026-10-13)

The user decided on the finding this branch reported: **take it, do not refuse it.** A Columns,
HeaderGroups or Sorts list handed again as the same instance, with elements that changed, is
treated exactly as if a new list had been handed. The column state the grid itself holds is carried
across by column Name, as AG Grid carries its column state by colId when columnDefs are re-applied.

**What the grid does now:**
- **Drawn from the copy (decided with the user on 2026-10-13).** The grid takes a read-only copy of
  each of the three lists when it is handed one, and draws from the copy. A Consumer's change to
  its own list shows when the list is handed again, never through a render of the grid's own (a
  scroll, a key) in between. Handing back the copy the grid exposes is no change.
- **One code path.** `SetParametersAsync` compares each handed list with the copy, element by
  element, by reference.
- **When the same instance arrives with other elements,** the grid takes a copy of it, a new
  instance. Everything keyed on the list's identity then rebuilds exactly as for a new list, and the
  skip is not taken.
- **Handed again unchanged,** the copy is kept, so nothing rebuilds and UX-5 still skips.
- **The Window** keeps its painted-rows check (ADR-0141). It is not copied, so the same reasoning
  does apply to it, and that is a question for the user. A row replaced in place in a pushed Window
  array, off screen, shows as soon as a scroll of the grid's own paints it, before the Consumer hands
  the array again. A row replaced in place on screen repaints when the array is handed again, by the
  painted-rows check. Copying the Window on each handing would cost a pass over the whole Window,
  which ADR-0141 refused, so it was not done.

**Column state, on any new or changed Columns list:**
- **Carried by Name:**
  - the column whose popover is open (closed if the column is gone);
  - the column a resize grip is dragging (the drag ends if the column is gone);
  - the Auto width measured for a column, kept for the same column instance under unchanged metrics
    (the whole Grid Metrics, not only the cell metrics) and measured afresh for any other instance
    (ADR-0003's note of 2026-10-11).
- **Ended, not carried:** a header reorder drag. It lifted a unit of columns by position, and a drop
  against the new order would report a move of other columns (principle 6).
- **Not carried, as ADR-0011 decides:** the Selection, the Copied Range and an open edit. They are
  matched by column names in order, and dropped when the visible columns change.
- **Not held by the grid, nothing to carry:**
  - Sorts and filters, which are the Consumer's or the Source's and keyed by Name;
  - column widths a user dragged, which the grid reports through `OnColumnWidthChanged` and the
    Consumer holds;
  - the pinned boundary, which is a parameter.
- **Derived, rebuilt as for any new list:** the header layout, the column geometry, the Cell
  Appearance cache and the rows' contents.

No ADR forbids any of this, so there is no state left over to propose.

### ADR-0003, a dated note under the note of 2026-10-11

old ->
```
  *(2026-10-11, decided with the user: the same holds for `Columns`. A new array re-renders every
  surviving row and the same instance re-renders none; the Definition of Done's RR-5, which asked
  that a fresh-but-equal array re-render nothing, was restated to match, since "equal" columns would
  call two `Format` delegates equal that paint differently.)*
```
new ->
```
  *(2026-10-11, decided with the user: the same holds for `Columns`. A new array re-renders every
  surviving row and the same instance re-renders none; the Definition of Done's RR-5, which asked
  that a fresh-but-equal array re-render nothing, was restated to match, since "equal" columns would
  call two `Format` delegates equal that paint differently.)*
  *(2026-10-13, decided with the user: the same instance with other elements counts as a change, for
  `Columns`, `HeaderGroups` and `Sorts` alike — a column added, replaced, removed or moved in place is
  taken as a new list, compared element by element by reference, so a fresh copy is what the grid
  holds and everything keyed on the list's identity rebuilds by the one path a new list takes. The
  column state the grid itself holds — the column whose popover is open, the column a resize grip is
  dragging, a column's measured Auto width for the same instance — is carried across by the column's
  Name, as AG Grid matches re-applied columnDefs by colId. What is derived from a definition is not
  carried across instances, by the note above. The Selection, the Copied Range and an open edit still
  go when the visible columns change (ADR-0011). A list handed to the grid is drawn from the copy the
  grid takes when it is handed; a Consumer's in-place mutation shows when the list is handed again,
  never through a render of the grid's own in between.)*
```

### Definition of Done, a new row after FN-21 (FN-22 is the first free FN number)

old ->
```
| **FN-21** | MUST | A Chrome seam's contents may hold an **Inner Popup** drawn outside the instance root. While one is open: Escape closes the Inner Popup first and the next Escape the popover; under the design system's default (`ModalOverlay = false`) a pointer-down elsewhere in the instance closes both and keeps its own meaning, and under `ModalOverlay = true` only the Inner Popup closes; closing the popover any way removes the Inner Popup; DOM focus returns to the root (ADR-0039) | Layer 2: the contents report the popup through `InnerPopupChanged`, and the gate is told; Layer 3 with `ExGrid.MudBlazor`'s filter panel — a `MudSelect` and a `MudDatePicker` — by pointer and by key, under both settings, with two grids on the page and with one inside a `MudDialog` | every clause observed; the other grid unaffected |
```
new ->
```
| **FN-21** | MUST | A Chrome seam's contents may hold an **Inner Popup** drawn outside the instance root. While one is open: Escape closes the Inner Popup first and the next Escape the popover; under the design system's default (`ModalOverlay = false`) a pointer-down elsewhere in the instance closes both and keeps its own meaning, and under `ModalOverlay = true` only the Inner Popup closes; closing the popover any way removes the Inner Popup; DOM focus returns to the root (ADR-0039) | Layer 2: the contents report the popup through `InnerPopupChanged`, and the gate is told; Layer 3 with `ExGrid.MudBlazor`'s filter panel — a `MudSelect` and a `MudDatePicker` — by pointer and by key, under both settings, with two grids on the page and with one inside a `MudDialog` | every clause observed; the other grid unaffected |
| **FN-22** | MUST | A `Columns`, `HeaderGroups` or `Sorts` list handed again as the same instance with other elements is taken as a new list — a column added, replaced, removed or moved in place repaints header and cells when the list is handed again, and never before, through a render of the grid's own: the grid draws from the copy it took when handed the list — and the column state the grid holds (an open column popover, a resize in progress, a measured Auto width of the same instance) follows its column by Name; the same list holding the same elements renders nothing (ADR-0003, note of 2026-10-13; ADR-0027 P3) | Layer 2: a Consumer changing its lists in place, the grid rendering for its own reasons, then the Consumer rendering | the old state painted until the list is handed again, then each change; the open menu on its column after a move; no render when nothing changed |
```

### Docs Site (pages are off limits to this branch)

`samples/ExGrid.Docs/Pages/Grid/GridData.razor`:

old ->
```
            rows, never rewrite them in place. And keep the source and the column list in fields, so they are the
            same objects on every render.
```
new ->
```
            rows, never rewrite them in place. And keep the source and the column list in fields, so they are the
            same objects on every render. A column list you change in place — a column added, replaced, removed or
            moved — shows when you hand it to the grid again, as a new list would: the grid draws from the copy it
            took when it was handed the list, and what it holds for a column follows the column by its Name.
```

