# Run 17 corrections, the Selection Summary: what the orchestrator writes

Branch `claude/run17-selection-summary`, from `claude/run17-corrections`. The seventeenth Windows run's Part B
(`verification/2026-10-11-windows-17/report.md`, b5 to b8; `records/part-b.jsonl`; `shots/part-b/b5-*` to `b8-*`)
read Excel's status bar under the gestures SM-7 and SM-9 wait on. One reading disagreed with ExGrid's family and is
corrected to Excel's answer; three agree and are pinned. Nothing here changes an ADR, `CONTEXT.md`, the Definition of
Done or the Docs Site: the text for each is below, in the repository's voice.

## What was done, case by case

| Case | Excel (run 17) | Was | Now | Why |
|---|---|---|---|---|
| b5: A1 `1`, A2 `2`, A3 `=1/0`, A4 `x`, A5 blank; A1:A5, all six figures ticked | `Count: 4    Numerical Count: 2`, and nothing else | `Count: 4` alone: Numerical Count was `ErrorAmongValues` and shown nowhere | **`Count: 4`, `Numerical Count: 2`**, Average, Min, Max and Sum shown nowhere | FOLLOW. ADR-0130: "confirmed in a Windows run before the criterion is signed off"; SM-7: "as Excel's status bar does, once a Windows run has read" |
| b6: A3 cleared; A1:A5 | `Average: 1.5  Count: 3  Numerical Count: 2  Min.: 1  Max.: 2  Sum: 3` | the same | the same, pinned | AGREE: SM-7's control |
| b7: A1 `1`, A2 `TRUE`, A3 `2026-10-11` (46306), A4 `x`; A1:A4, A1:A2, A3 | A1:A4 `Average: 23153.5  Count: 4  Numerical Count: 2  Min.: 1  Max.: 46306  Sum: 46307`; A1:A2 `Average: 1  Count: 2  Numerical Count: 1  Min.: 1  Max.: 1  Sum: 1`; A3 alone no figure | the same | the same, pinned | AGREE: ADR-0130's table (a Boolean counted, not numerically), its refinement "ExSheet's dates are numbers", and SM-9 (two or more cells) |
| b8: A1 `#,##0.00` `1000`, A2 `0%` `2000` (20 by automatic percent entry); A1:A2 from A1, then from A2 | from A1 `Average: 510.00  Count: 2  Numerical Count: 2  Min.: 20.00  Max.: 1,000.00  Sum: 1,020.00`; from A2 `Average: 51000%  …  Min.: 2000%  Max.: 100000%  Sum: 102000%` | the same | the same, pinned | AGREE: ADR-0130's refinement of 2026-10-06 (ExSheet writes Average, Min, Max and Sum in the Focus cell's Number Format) |

**The rule taken, not only the case.** An error among the values is counted and is never a number, so it is in Count
and not in Numerical Count, and both counts are shown; every figure that reads numbers (Average, Min, Max, Sum, and
ExPivot's Product and deviations through the same accumulator) is `ErrorAmongValues` and shown nowhere. The change
is one line in `AggregateAccumulator.Read`: `Aggregation.CountNumbers` joins `Aggregation.Count` among the
Aggregations an error leaves standing. It reaches every answerer that folds an error in:

- **ExSheet** (`SheetSummary`, an Error Value);
- **ExPivot**, laid out in the browser (`PivotSummary`, a `#DIV/0!` or `#NUM!` cell) and answered by a server
  (`ExPivot.Operations`' `SummarizeReportAsync`: the server's `HasError` flag is folded in as one already-counted
  cell, and its `Counts.Numbers` — which `LocalPivotReportSource.SummaryAsync` never counts an error in — is read
  as Numerical Count);
- **a Consumer's own answerer** through `GridSummaryCells.AddError`.

**Confirmed, as the triage asked:** `AddError` is called only by the summaries — `src/ExSheet/SheetSummary.cs`,
`src/ExPivot/PivotSummary.cs`, `src/ExPivot/Components/ExPivot.Operations.cs` and `GridSummaryCells` — and never by
ExPivot's engine. `ExPivot.Engine` reads its Value Fields through `AggregateArithmetic.Read`, which has no error
among values, and nothing in `src/` but `AggregateAccumulator` produces `AggregateError.ErrorAmongValues`. A pivot
cell's Aggregation is unchanged. `GridSource.From` never folds an error in (a row's value that is not a number is
counted as text), so a grid bound to it is unchanged too.

**ExGrid's "Focus's column's format" stays.** Excel's rule (b8) is the active cell's format for every figure but
the two counts, whatever the other selected cells' formats. ExSheet and ExPivot follow it to the cell; a grid's
formats are its columns', so the Focus's column's format is the same rule on a grid.

Tests, each named with its ADR:

- `tests/ExGrid.Data.Tests/AggregateAccumulatorTests.cs`: `ADR0130_an_error_among_the_values_leaves_only_the_two_counts`
  (rewritten from `…_leaves_count_alone`, which expected `ErrorAmongValues` from Count Numbers; it now expects 2,
  and the error from all nine Aggregations that read numbers), and `ADR0130_an_error_beside_merged_parts_leaves_only_the_two_counts`
  (ExPivot's server path: a flag and merged parts).
- `tests/ExGrid.Tests/GridSummaryTests.cs`: `ADR0130_an_error_among_the_cells_leaves_count_and_numerical_count`
  (b5 through `GridSummaryCells`, no answerer text on the figures shown nowhere) and
  `ADR0130_without_the_error_every_figure_is_shown` (b6).
- `tests/ExSheet.Components/SheetSummaryTests.cs` (new): b5, b6, b7 and b8 on a rendered Sheet with all six figures
  shown, by the run's own gestures (typing, Delete, the Name Box, Shift+arrow from the active cell), each figure's
  text Excel's verbatim. The b5 test fails without the fix (`["Count: 4"]`), and passes with it.

## ADR-0130: what to write

**A new section**, after "The walk of a new Window":

> ## Read in a Windows run *(2026-10-12)*
>
> The seventeenth Windows run read Excel's status bar under the gestures this decision left to it
> (`verification/2026-10-11-windows-17/`, Part B's b5 to b8), all six figures ticked.
>
> - **An error among the values leaves Count and Numerical Count.** Over `1`, `2`, `=1/0`, `x` and a blank, Excel
>   shows `Count: 4    Numerical Count: 2` and no other figure (b5); with the error cleared, all six (b6). An error
>   is counted and is never a number, and every figure that reads numbers is shown nowhere. The family followed:
>   `Aggregation.CountNumbers` answers beside `Count` where an error is among the values, in ExSheet, in ExPivot
>   (its own cells and a server's parts alike) and through `GridSummaryCells`. *(It read "leaves only Count", as
>   Excel was understood to do; the run showed Numerical Count beside it.)*
> - **A Boolean is counted and not numerically, and ExSheet's dates are numbers**, as the table and the refinement
>   above say: over `1`, `TRUE`, the date 46306 and `x`, Excel shows `Count: 4`, `Numerical Count: 2`, `Max.: 46306`
>   and `Sum: 46307` (b7). One cell shows no figure.
> - **Every figure but the two counts wears the active cell's format, whatever the other cells' formats.** Over
>   `1,000.00` and `2000%`, Excel shows `Sum: 1,020.00` from the first and `Sum: 102000%` from the second (b8). This
>   is the rule ExSheet and ExPivot already followed, to the cell; a grid's formats are its columns', so the Focus's
>   column's format is the same rule on a grid, and it stays.

**And in place, with the change said:**

- "What each figure means", the bullet "**An error among the values leaves only Count.** That is what Excel is
  understood to do; it is confirmed in a Windows run before the criterion is signed off." becomes:
  "**An error among the values leaves Count and Numerical Count**, and no other figure: Excel's status bar, as the
  seventeenth Windows run read it (below). *(It first read "leaves only Count", what Excel was understood to do.)*"
- The bullet "**The figures are formatted with the Focus's column's format** until Excel's own rule is read in a
  Windows run; then they follow Excel." becomes: "**The figures are formatted with the Focus's format**: Excel's
  rule, read in a Windows run (below), is the active cell's format for every figure but the counts. On a grid the
  Focus's format is its column's."
- The table's error row is right as it stands (`counted | — | no figure`): the error is not in Numerical Count,
  and Numerical Count is still shown. To keep the table from being read as "no Numerical Count figure", its last
  column could be headed "Sum, Average, Min, Max (the figures that read numbers)".
- "Refined while implementing", the format bullet's last sentence "the Windows run still reads what Excel does
  across mixed formats" gains: "*(Read 2026-10-12: the active cell's format, whatever the others'.)*"
- "SM-7 and the format rule were implemented without waiting for the Windows run … Both stand to be corrected by
  the run." gains: "*(The run, 2026-10-12, corrected SM-7 — Numerical Count stands beside Count — and confirmed the
  format rule.)*"

## Definition of Done: what to write

- **SM-7**, the criterion: "An error among the values leaves Count and Numerical Count, and no other figure, as
  Excel's status bar does (ADR-0130). *(2026-10-12: Excel's rule recorded in `verification/2026-10-11-windows-17/`,
  Part B's b5 and b6; the criterion read "leaves Count alone … once a Windows run has read Excel's answer" until
  then.)*" Test column: "Layer 1 (`AggregateAccumulatorTests`, `GridSummaryTests`), Layer 2
  (`SheetSummaryTests`); the Windows record". Pass: "as recorded".
- **SM-9**, "formatted by the Focus's column's format until Excel's rule is recorded" becomes "every figure but the
  counts in the Focus's format — Excel's active cell's, whatever the other cells' formats: the Focus cell's on a
  Sheet and a report, the Focus's column's on a grid". Dated note: "*(2026-10-12: Excel's rule recorded in the
  seventeenth Windows run, Part B's b7 and b8, and pinned in `SheetSummaryTests`.)*"

## Docs Site: what to write

- **`/exsheet`**, `Pages/Sheet/SheetOverview.razor`, "Status-bar figures" (line 134): "An error among the cells
  leaves Count alone." becomes "An error among the cells leaves Count and Numerical Count, and no other figure, as
  in Excel. Average, Min, Max and Sum wear the active cell's Number Format, whatever the other cells' formats."
- **`/exgrid/selection`**, `Pages/Grid/GridSelectionAndKeys.razor`, "Status-bar figures", the **What counts.**
  bullet gains: "An answerer that holds error values — a Sheet, a pivot report, your own through
  `GridSummaryCells` — counts an error in Count and never in Numerical Count, and Average, Min, Max and Sum are then
  shown nowhere, as in Excel. Those four wear the format of the Focus's column, as Excel's wear the active cell's."
- **`/expivot/layout`**, `Pages/Pivot/PivotLayoutPage.razor`, "Report forms, subtotals and grand totals", the
  paragraph on the line under the report gains: "An error value among them leaves Count and Numerical Count, as in
  Excel, and the figures wear the format of the Focus cell's Value Field."

## Change Notes written

`docs/releases/unreleased/`, one per product reached, each `Kind: Behaviour`, `ADR: 0130`:

- `run17-selection-summary-error-counts-exgrid.md` (`Docs: exgrid/selection#status-bar-figures`);
- `run17-selection-summary-error-counts-exsheet.md` (`Docs: exsheet#status-bar-figures`);
- `run17-selection-summary-error-counts-expivot.md` (`Docs: expivot/layout#report-forms--subtotals-and-grand-totals`);
- `run17-selection-summary-error-counts-data.md` (no page shows `AggregateAccumulator`, so no `Docs:` line).

The triage named one note for ExGrid, ExSheet and ExPivot, `Changed`: ADR-0190's checker takes one `Product` per
note ("A change that reaches two products has a note for each") and `Behaviour` for a change of what is shown.

## Files outside the brief's list

- `src/ExPivot/PivotSummary.cs`: its doc comment said an error "leaves Count alone". Comment only.
- `src/ExGrid/Components/ExGrid.Summary.cs`: the private `SummaryText`'s doc comment said the Focus's column's
  format holds "until Excel's own rule is read in a Windows run". Comment only.

## Not acted on

- **Excel's names `Min.` and `Max.`** (b6 to b8, `shots/part-b/b8-3-a2-a1-from-a2-status.png`) beside the grid's
  built-in `Min` and `Max` (`BuiltInCommandLabels`). The run's Excel was an English (UK) one; whether an English
  (US) Excel writes the dot was not read, and the built-in words are a Chrome's, replaceable through `Label`. A
  run on an English (US) Excel settles it.
- **Layer 3.** `tests/ExGrid.Browser/selection-summary.spec.mjs` covers `/features` (a grid bound to
  `GridSource.From`), not a Sheet; see the report for the Sheet case added there and its run.

## For a run: what the status bar was not asked

Written as Part B steps, all six figures ticked as in b5:

| # | What is done | What it settles |
|---|---|---|
| SM-a | A1 `x`, A2 `y`; A1:A2 | Whether Excel shows `Numerical Count: 0` where no cell is a number (the grid shows it), or leaves it out |
| SM-b | A1 `=NA()`, A2 `x`; A1:A2 | An error with no number: `Count: 2` and `Numerical Count: 0`, or Count alone |
| SM-c | A1 `=NA()`, A2 `=1/0`; A1:A2 | Errors only: as SM-b |
| SM-d | A1 `1`, A2 `=NA()`, A3 `2`; A1:A3, active A2 (a press on A2, then Ctrl+press on A1 and A3, or Shift+press from A2) | Whether an error in the active cell changes anything (it has no format of its own) |
| SM-e | A1 `1`, A2 `2` in General, A3 `3` formatted `0.0`; A1:A3 from A1, then from A3 | The format rule with General in the active cell beside a formatted one, both ways (b8 had a format on both) |
| SM-f | A1 `1/3` (`=1/3`), A2 `1`; A1:A2 in General | How many digits a General Average shows (`0.666666667`, ten significant digits as the grid writes a `double`, or another width) |
