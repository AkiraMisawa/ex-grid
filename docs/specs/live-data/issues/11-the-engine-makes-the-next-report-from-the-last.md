# 11: The engine makes the next cube and report from the last

Status: done

**What to do:** build the core of [ADR-0161](../../../adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md). Under the same layout, a data redraw starts from the
cube and report on screen and the leaves that changed. It keeps the axis nodes, computes again the cells
on the changed paths, and shares every row whose path did not change. Nothing is rewritten in place.

**Blocked by:** 09, 10

## What to build

- **Which leaves changed:**
  - `PivotAnswer` gains the optional changed leaves since a Source Version the question names
    (ADR-0066's note), and `SnapshotPivotSource` supplies them from its fold.
  - Without them, the engine compares the leaves of the new answer with the previous one. Measure what
    that comparison costs at 400,000 leaves.
- **The next cube** shares the axis trees and copies the values it changes. **The next report** shares
  the rows off the changed paths.
- **The redraws that start afresh**, exactly ADR-0161's list: data-free changes, a change of structure,
  a batch the source could not fold, an order that follows values, a Show Values As that reads other
  rows, and the engine's own threshold of changed leaves.
- **`ExPivot.Asking.cs`** takes the path. PV-40's slicing still holds, and the report on screen stays
  until the next is complete.
- **Change Highlight:** compare only the rows on changed paths when the redraw was incremental (10's
  rule, narrowed).

## Done when

- [x] PV-44's property test passes over random batches and layouts, both with the source's changed
  leaves and without; the seed recorded
- [x] PV-45 passes: the rows that render per redraw are the painted rows whose text changed
- [x] LV-22's ExPivot half passes: no report but the one on screen alive after a run of redraws
- [x] Layer 1 and 2 green; the pivot layer-3 specs pass locally on one host

## Comments

2026-10-07: Built.
- **Which leaves changed.** `PivotQuery.ChangedSince` names the version of the answer the asker holds.
  It takes no part in the question's equality, and JSON carries it. `PivotAnswer.ChangedLeaves` says
  which leaves changed since that version (`PivotLeafChanges`): either the same leaves with the ones
  named changed, or leaves made afresh. Any source can say it (`WithChangedLeaves`), and JSON carries
  it. `SnapshotPivotSource` says it from its fold: the leaves the batches folded in since its last
  answer touched, when the answer holds the same leaves in the same order under the same Items spelled
  the same. Otherwise it says the leaves were made afresh, as after a compaction or a batch past the cap.
  A question that names any other version is answered without it.
- **The next cube** (`PivotEngine.NextCube`, `NextCubeAsync`) shares the cube on screen's axis trees,
  cells and values. It computes again each changed leaf's cell and every total on its paths, each from
  every leaf beneath it in the leaves' order, then writes each exact value in one form, as a cube built
  afresh does. Its changed values are held apart by cell over the shared ones. Once they pass an eighth
  of the cells (and at least 4,096), they are copied once into values of the cube's own. When the answer
  names no leaves, each leaf's parts are compared with the cube's: a decimal of another value, or a
  double of other bits, is a change.
- **The next report** (`NextReport`, `NextReportAsync`) shares the columns and every row whose painted
  text did not change, and makes the others anew. It keeps the cells it found for `ChangesSince`, so the
  Change Highlight compares only those rows (ticket 10's rule, narrowed). `WasMadeFrom` lets ExPivot
  keep the label widths.
- **Afresh, as ADR-0161 lists:**
  - a change that is not data: ExPivot makes a report from the last only for data under the layout and
    words on screen, and the engine checks both;
  - a change of structure: the leaf count, the Items as spelled, each leaf's Items;
  - a batch the source could not fold: the source says the leaves were made afresh, and a new source is
    another source;
  - an order by a Value Field;
  - any Show Values As but No Calculation. The first version shares under none of them: % of row total
    reads only its own row, but ADR-0161 names "% of a total" among those that start afresh;
  - the engine's threshold: more than a quarter of the leaves, and more than 16, changed.
- **ExPivot** asks a data question naming the version of the cube on screen, and makes the next cube
  and report from the ones on screen (`ExPivot.Asking.cs`). PV-40's slices still hold, and the report on
  screen stays until the next one is complete.
- **PV-44** is `NextReportTests.The_next_report_equals_one_built_afresh`, seeds 1 to 8, recorded in the
  cases. Each seed runs six layouts of fifteen Change Batches over 400 keyed trades: every form, both
  values axes, each Aggregation in turn across the seeds, a Show Values As or a hiding format now and
  then, subtotals on and off, at the top and the bottom, collapsed and hidden Items, and sorts by label and
  by value. Each redraw runs twice: as the source named its leaves, and as the engine compared them.
  Zero differences, and a row is shared exactly when its painted text did not change. Beside it:
  - `Values_changed_over_many_redraws_are_folded_into_a_cube_of_its_own`;
  - `The_next_cube_and_report_are_made_in_slices`;
  - `ChangedLeavesTests`;
  - the JSON round trip.
- **PV-45 and LV-22's ExPivot half** are `LiveRedrawTests`, on a copy of `/pivot-live`'s generator.
- **Measured:** the next cube at 400,000 leaves (a thousand books by four hundred dates, two Sums),
  one leaf changed. Release, `DOTNET_TieredCompilation=0`, least of 15 runs, Apple M4 Pro:
  - 7.6 ms when the source names the leaf;
  - 12.2 ms when the engine compares, so the comparison costs about 4.6 ms;
  - 36.5 ms for the cube built afresh.
  The test is `Measurements.The_next_cube_at_four_hundred_thousand_leaves`. The full before and after
  is ticket 13's.
- **Layers 1 and 2:** 9,086 passed, 9 skipped.
- **Layer 3:** the five pivot specs on the WebAssembly host, Chrome, headless: 108 passed and 7
  skipped. The 7 are `measure-pivot`'s, which run only when asked. This ticket's state was run with the
  review's fixes (the next commit) on it, before and after the second review pass.

2026-10-07 (review): as the orchestrator decided, `NextCube`, `NextReport` and their sliced forms,
`StartsAfresh`, `ChangesSince`, `PivotReportChanges`, `RowFor` and `WasMadeFrom` are internal. ExPivot sees
them through `InternalsVisibleTo`; ExPivot.MudBlazor and the samples need none of them. Public, for a
Consumer or a server's source: `PivotReport.ValueAt`, `PivotQuery.ChangedSince` and `WithChangedSince`,
`PivotAnswer.ChangedLeaves` and `WithChangedLeaves`, and `PivotLeafChanges`. A layout that orders by a
Value Field or has a Show Values As builds its cube afresh too, so those redraws share nothing. An answer
that carries other parts than the cube keeps starts afresh. PV-44 now also chains across every layout
change, sharing no row there, and new words and a new culture share none
(`New_words_or_a_new_culture_share_no_row`). `LiveRedrawTests.A_new_source_or_a_new_layout_shares_no_row`
covers ExPivot. Layers 1 and 2: 9,093 passed, 9 skipped.
