# 09: A report row holds no value and no report

Status: ready-for-agent

**What to do:** build the "What a report row is" part of [ADR-0161](../../../adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md). A `PivotReportRow` holds what
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

- [ ] No report row points at a report. The engine's and ExPivot's suites pass unchanged in meaning,
  with tests moved to the new API
- [ ] PV-43's allocation test still reads zero bytes for the keys
- [ ] Layer 1 and 2 green; the pivot layer-3 specs pass locally on one host

## Comments
