# 12: The component asks the source

Status: done

**What to build:** `ExPivot` takes a `Source` instead of records (ADR-0059, ADR-0066).

- **While a question is out:** the Field List shows the new layout; the report stays as it was,
  under `IsLoading`; a further change cancels the question; and an answer to a superseded question
  is discarded.
- **A change that needs no new question asks none.**
- **Caps:** a layout that breaks one is refused by name, and the report stays on the layout before.
- **Defer Layout Update:** a checkbox and an Update button at the pane's foot.
- **Value Field Settings…** disables the Aggregations the source does not offer, giving the reason.
- **A refused Source Version** makes ExPivot say that the data has changed.

**Blocked by:** 10

- [x] PV-25, PV-26 with a source that answers on demand and counts questions
- [x] PV-28: Defer Layout Update
- [x] PV-29: the caps, in the component
- [x] PV-24: the unsupported Aggregations disabled, with the reason
- [x] PV-23: the component's side
- [x] PV-2, PV-5, PV-8, PV-9, PV-11, PV-13, PV-15, PV-17 still green

## Comments

2026-10-01: Built. `ExPivot` takes a `Source` and is no longer generic. Asking never blocks: a
layout the held answer covers (`PivotCube.Holds`, part-aware) is laid out at once and asks
nothing; otherwise the question goes out, the Field List shows the new layout at once, and the
report stays as it was under the grid's `IsLoading`. A further change cancels the question in
flight, and a generation counter discards an answer to a superseded question, late or early. A
field in Filters that hides nothing does not travel (`PivotQuery.For`, `PivotCube.Holds`). A
failure, a failed Refresh included, leaves the report as it was and is said under the toolbar
(`LastError`). `PivotCaps` holds the leaves, the rows and the columns: a layout that breaks one
is refused by name under the toolbar, and the layout goes back to the one before. Defer Layout
Update and Update stand at the pane's foot. Value Field Settings… offers the Aggregations the
source does not answer disabled, with the reason, and never asks for them. Filter… and the band
list Items under the report's Source Version, an open Filter… lists again when a new version
lands, and a refused version says the data has changed. Layer 2: `AskingTests`,
`ToolbarAndDeferTests`, `DetailsAndVersionTests` and `SubstitutedChromeTests` in
`tests/ExPivot.Components`, and `tests/ExPivot.MudBlazor.Tests`; layer 3: `pivot.spec.mjs` on
the WebAssembly host in Chromium, under both Chromes. The Stale Report's notice and the source's
`Changed` are ticket 15's: the failure ExPivot holds and `AskAgainAsync` are where it starts.
