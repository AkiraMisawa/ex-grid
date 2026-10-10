# 10: The report's Change Highlight keeps times, not reports

Status: wontfix — built on the Claude Code track on 2026-10-07, and replaced on 2026-10-08 when the
user took the Codex track's ExPivot (ADR-0151 to ADR-0153). None of what this ticket names is in the
merged code; ADR-0161's table says what replaced each part.

**What to do:** build the Change Highlight part of [ADR-0161](../../../adr/0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md).
- ExPivot hands the grid one `CellChangedAt` for as long as a history lasts.
- The history holds the time each cell's painted text last changed, by row key and value column, and no
  report.
- The times are set when a data redraw is laid out:
  - the rows on a changed path are compared with the previous report's painted text;
  - a row new to the report is marked whole;
  - a redraw laid out afresh under data compares every row, in slices (PV-40).
- A mark ends with its time.

**Blocked by:** 09. Until 11 builds the changed paths, compare every row on every data redraw: the rule is
the same, and 11 narrows it.

## Done when

- [x] PV-46 passes (§29), with a fake `TimeProvider`; PV-36 still passes
- [x] After a burst of data then quiet, the history holds no report, and marks end on time
- [x] Layer 1 and 2 green; `/pivot-live`'s layer-3 spec passes locally

## Comments

2026-10-07: Built. `ReportHistory` keeps the time each value cell's painted text last changed, by row key
and value column name, and the times rows and columns appeared, and holds no report. ExPivot hands the
grid one `CellChangedAt` for as long as a history lasts: a history starts with the report a layout, a
sort, a collapse, a form, Show Values As, a format or new words laid out, and data only records into it.
The delegate is null only while the duration is zero; a duration that becomes zero, or stops being zero,
starts the history again. The engine says what changed: `PivotReport.ChangesSince(earlier)` and its
sliced form pair rows by key and columns by name. They return the cells whose painted text differs (two
values alike to the last bit are not formatted), the rows and columns that appeared, which are marked
whole, and the rows and columns that left, whose times go. Until ticket 11, every data redraw compares
every row, in slices, while the report is laid out. A time whose mark has ended answers nothing, and the
times are let go only as new ones are recorded. A key is kept as the newest row recorded under it, so a
time that keeps moving does not hold an old report's axis tree. `PivotSummary`'s format no longer holds
the report it was made from. Tests: `ReportChangesTests` (layer 1) and, in `ChangeHighlightTests`,
`One_delegate_for_as_long_as_a_history_lasts`, `A_mark_ends_with_its_time_without_further_data`,
`After_a_burst_then_quiet_the_history_holds_no_report` and
`A_redraw_laid_out_afresh_compares_every_row`. PV-36's tests stand; where they asserted that a gesture
leaves the grid no delegate, they now assert a new one that marks nothing. Layers 1 and 2: 9,065 passed,
8 skipped.
Layer 3: `/pivot-live`'s spec on the WebAssembly host, Chrome, headless, on this ticket's own state: 5 passed.

2026-10-07 (review): a time is let go when its mark ends, by a timer on ExPivot's clock, and not only
when new data comes (ADR-0161: "An entry goes when its time is over"). The timer is re-timed on a new
clock and disposed with the pivot. With a duration of zero nothing is compared or recorded. A duration
that becomes zero, or stops being zero, starts the history again. When another report goes up during a
build, the build compares again, in slices, and `Show`'s unsliced fallback is gone. `ChangesSince` and
`PivotReportChanges` are internal. Tests: `A_duration_that_stops_or_starts_being_zero_starts_the_history_again`,
`Times_are_let_go_on_the_clock_handed_in`, `A_removed_pivot_disposes_its_timer`, and the burst test,
which now also asserts that no time is kept.
