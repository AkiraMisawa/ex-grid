# The grid holds no Consumer row beyond the Window it was given

*(Decided with the user on 2026-10-07, in the grilling of live data continued — Q3 and Q6 — after the
cause of a live ExPivot's out-of-memory was found:
[`2026-10-06-macos-pivot-oom`](../../verification/2026-10-06-macos-pivot-oom/README.md).)*

[ADR-0140](./0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md)
says the grid "holds nothing between Windows", and principle 3 says the grid does not hold the data. The
code did not keep to either:

- **The kept paints held their row instances**, up to 64 renders back, for
  [ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md) as
  first decided.
- **The Cell Appearance cache held up to about 256 row instances** from whichever render last painted
  each position (`CellAppearances`, ADR-0050 item 15), whenever a Consumer declared `CellAppearance`.

A row is the Consumer's object, and the grid cannot know what it points at. An ExPivot report row points at
its whole report and cube. A row view over a Snapshot would point at the Snapshot. So a few old rows can be
whole generations:

- At 101,001 report rows, each live redraw left 41 MB reachable after a full collection, without end.
- A live ExPivot ran out of the 2 GB WebAssembly heap on the 7th redraw of a 401,001-row report.
- Over a Fluxor store, the same paints kept 630 old trade versions. They were harmless only because those
  rows point at nothing
  ([`2026-10-06-macos-fluxor-spike`](../../verification/2026-10-06-macos-fluxor-spike/README.md), check 6).

**The rule: the grid holds a Consumer's row instance, and its Row Key, only while it is in the Window the
grid was last given.** Anything kept longer is kept as positions, strings or numbers.

## What follows

- **The kept paints are gone.** ADR-0142, as rewritten, judges no write against what an earlier render
  painted. The layouts of recent renders stay, as positions and geometry, so that a press lands on the
  cell it was made on (ED-31).
- **The Cell Appearance cache forgets a row once it leaves the Window.** A new Window drops every entry
  whose row instance it does not hold; what is kept for the rows it does hold is their appearance values.
- **Three holdings outlive a Window, bounded to one each, and are named here so they are not mistaken for
  leaks:**
  - **The Window last measured for Auto widths** (ADR-0016), while a fling or a hidden or zero-sized
    Viewport defers the next measure. It is replaced at the next measure.
  - **The row of an Action press in flight**, until its click is heard or the core answers the press
    (ADR-0142, "No press is lost to Blazor").
  - **The Row Key of the row under an open editor**, while the editor is open, so that the editor can
    follow its row through an order move and a commit can tell that the row is gone
    ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)'s note of
    2026-10-07). *(Added the same day.)*
- **Blazor's renderer keeps the frames of the render before the newest** as the buffer it renders into
  next, and those frames hold the painted rows' parameters. So after the render that takes in a new
  Window, the rows the previous render painted stay reachable until the grid renders again. That is the
  framework's, not a holding of the grid's, and it is bounded to one render's painted rows. Rendering
  once more after each new Window to clear it was considered and not taken: it is a render of the whole
  grid per live update, to free a few dozen rows. *(Found while building it, 2026-10-07: the rows were
  traced to `ComponentState`'s next render tree, and to nothing of the grid's.)*
  - **Under ExPivot the same holds one level up.** The frames of `PivotGridHost`'s render before the
    newest name the whole Window list it handed the grid, painted or not, until ExPivot renders
    again. A highlight that ends renders only the grid. The holding is bounded to one Window, and its
    rows reach no report (ADR-0153). *(Found while building LV-22's ExPivot test, 2026-10-08.)*
- **What a Consumer hands the grid as its own object is the Consumer's to hold**: a Row Mark adapter, a
  Pointed At's dashes (ADR-0058), a popover's content. The grid keeps the reference, not rows of its own.

## How it is checked

- **By reachability, not by megabytes.** A heap size moves with the runtime and the machine, as a time
  does, so it never gates (§1).
- **Layer 2, a MUST**: after a run of new Windows, with weak references taken to the row instances of
  each earlier one and a full collection made, none of them is alive, apart from the three holdings above and the rows the render before the newest
  painted, which Blazor's renderer keeps.
  For ExPivot, after a run of live redraws, nothing is alive but what its report source keeps by
  design ([ADR-0153](./0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md)'s
  "What the history holds"):
  - the reports the Change Highlight compares, and the `versionsKept` newest;
  - the display rows of the Window on screen;
  - the display rows of the Window last served under the Report Version before it, which is the
    Baseline Window of the next Window Changes ([ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).

  Once the pivot renders again, the grid holds no display row of an earlier Window. *(Restated on
  2026-10-08, when LV-22's ExPivot test was built. The first wording promised no earlier Window's rows,
  and no report beyond the versions kept and the one on screen; the design taken that day promises
  neither. Under the Claude Code track's ADR-0161 the highlight kept no report. **Decided with the user
  on 2026-10-09**, in the grilling of the merged pull request: the report source may keep the reports the
  Change Highlight compares, within the bound ADR-0153 states, because the mark is a state of the cell
  that a cell scrolled into view must show; ADR-0153's "Why reports, and not times" gives the reasons and
  the options rejected.)*
- **Observational**: the loop of the out-of-memory record — 101,001 report rows, 20 live redraws in the
  browser, the managed heap after a full collection after each — is recorded in `metrics.json`. It
  levels off: at 89.5 MB, one report, on the Claude Code track's ExPivot
  ([`2026-10-07-macos-live-update-costs-after`](../../verification/2026-10-07-macos-live-update-costs-after/README.md));
  on the merged code at 137.6 MiB, flat from the 6th redraw, once the reports the Change Highlight compares
  reach ADR-0153's bound, where `main` grew by 41.2 MiB a redraw in the same run
  ([`2026-10-10-linux-merged-live-costs`](../../verification/2026-10-10-linux-merged-live-costs/README.md)).

## Considered options

- **Keep row instances, bounded by a count of Windows.** Rejected: what stays is still set by what a row
  points at, which the grid cannot see.
- **Keep row instances by weak reference.** Rejected: whether a paint could still be judged would then
  depend on when the collector ran, and an outcome never depends on timing (principle 6).
- **Have ExPivot's rows stop pointing at their report, and keep the grid as it was.** Taken for its own
  reasons — in ADR-0161 first, and in ADR-0153's detached display rows since 2026-10-08 — but not
  instead of this rule: it mends one Consumer, and the next row type that points at its container meets
  the same leak. The Codex track took only that path, and its grid still held about 256 rows in the Cell
  Appearance cache when the two tracks were compared.

## Consequences

- **ADR-0140's "holds nothing between Windows" is now a rule that can be checked**, and ADR-0050 item 15's
  cache keeps to it.
- **The Definition of Done gains LV-22 and LV-23** (§32).
