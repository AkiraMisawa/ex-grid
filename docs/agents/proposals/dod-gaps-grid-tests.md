# Proposals from claude/dod-gaps-grid-tests

## UX-5 and LV-22: a Consumer render that changes nothing no longer renders the grid

**Decided with the user on 2026-10-12:**
- Take the skip, so that UX-5 holds as written.
- Check LV-22 at ADR-0160's own bound.

**Implemented on this branch:**
- `ExGrid.SetParametersAsync` returns without rendering when every parameter it is handed is one it
  already holds: the same instance for a reference type, an equal value for a value type. An
  empty push still renders. The test is
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
                costs no render — and a list you already handed it, changed in place, repaints nothing: hand a new one.
```
