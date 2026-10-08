# 09: A report row holds no value and no report

Status: done

**What to do:** build the "What a report row is" part of [ADR-0161](../../../adr/0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md). A `PivotReportRow` holds what
it stands for (role, Value Field, Items, labels, its key) and no value and no report. A value cell is
asked of a report.

**Blocked by:** None. 10 and 11 build on it.

## What to build

- **`src/ExPivot.Engine/PivotReport.cs`:**
  - `PivotReportRow.ValueAt` and `PivotReportRow.Report` (with `Attach` and `_cells`) move to the report,
    as `PivotReport.ValueAt(row, column)`. The report computes a cell on first read and keeps it.
  - Rewrite the contract at `:239` to "a next report shares the rows it did not change".
- **ExPivot's column accessors read values through the report on screen.**
- **The checks that a row is the report on screen's** compare keys: `ExPivot.Report.cs` (the context
  commands, `ReferenceEquals(context.Row.Report, report)`) and `ExPivot.Details.cs:43`.
- **`ReportHistory` and every other reader of `row.Report` follow.**
- **ExPivot.MudBlazor and the samples follow the API.**

## Done when

- [x] No report row points at a report. The engine's and ExPivot's suites pass unchanged in meaning,
  with tests moved to the new API
- [x] PV-43's allocation test still reads zero bytes for the keys
- [x] Layer 1 and 2 green; the pivot layer-3 specs pass locally on one host

## Comments

2026-10-07: Built. A `PivotReportRow` holds its role, Value Field, Items (its `Key`) and labels, and no
value and no report: `Report`, `Attach`, `_cells` and `ValueAt` are gone from it. A value cell is asked of
the report, `PivotReport.ValueAt(row, column)`, which computes it on first read and keeps it, and refuses
by name a row of another answer rather than read another cell. `PivotReport.RowFor(key)` finds the
report's row for a key, indexed on first use. `DetailsQuery`, ExPivot's context commands and Show
Details compare keys with the report on screen. The contract in `PivotReport.cs` reads that a next report
shares the rows it did not change. ExPivot's column accessors read the report on screen, and
`ReportHistory`, `PivotSummary`, the engine's README, the package smoke and the `PivotRedraw` spike follow
the API. ExPivot.MudBlazor reads no row value and needed no change. Tests: `ReportRowTests` (a value
asked of the report, empty cells, a row holds no report by a weak reference, a row of another answer
refused, `RowFor`, Show Details by key) and
`ReportCommandTests.A_command_made_before_a_redraw_acts_on_the_row_on_screen`. Every test that read
`row.ValueAt` now reads `report.ValueAt`, and `SlicedWorkTests` drops its check that rows point at their
report. PV-43's allocation test is unchanged and green. Layers 1 and 2: 9,055 passed, 8 skipped.
Layer 3: the five pivot specs (`pivot`, `pivot-live`, `pivot-csv`, `pivot-db`, `pivot-risk`) on the
WebAssembly host, Chrome, headless, on this ticket's own state: 108 passed and 7 skipped. The 7 are
`measure-pivot`'s, which the file filter also names and which run only when asked.

2026-10-07 (review): `PivotReport.ValueAt` reads a row of another answer by its key — this report's row
that stands for the same thing — and nothing when there is none, rather than refuse it inside a grid's
render. `RowFor` is internal, as the orchestrator decided, and finds a row the report holds without
indexing it, so a Context Menu does not build an index of 401,001 keys. The report's lazy state is safe
to read from any thread. A Show Details command made before a redraw finds its column by name and
does nothing when the column has gone (`ReportCommandTests`).
