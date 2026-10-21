# ExBoard

Status: ready-for-agent

A Board is JSON an AI writes once and a workflow runs without AI: views of the family laid out in rows
and columns, linked by values, with outputs raised on a confirm and the user's changes raised as one
Board Diff. Decided in [ADR-0340](../../adr/0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md),
[ADR-0341](../../adr/0341-a-board-input-declares-its-columns-and-a-source-answers-its-shape-first.md) and
[ADR-0342](../../adr/0342-a-grid-builds-its-columns-at-run-time-from-a-sources-shape.md), with the
Chart Spec made one view of a Board ([ADR-0320](../../adr/0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md)).
The grilling's record is `exchart/grilling-family-ai.md` in the project's shared files.

## Where each part lives (ADR-0340, Q8)

| Part | Package |
|---|---|
| Board Input: declaration, shape check, JSON to Snapshot | `ExGrid.Data` |
| A grid's view read from a spec (Shaped Grid) | `ExGrid` |
| A pivot's view read from a spec | `ExPivot.Engine` |
| A chart's view read from a spec (the Chart Spec) | `ExChart.Engine` |
| The Board: reader, layout, links, outputs, Board Diff, tool definition | `ExBoard` (new), `ExBoard.MudBlazor` (new) |

No Engine gains a reference. The family holds no workflow: it reads a Board, checks its inputs, draws
it, and raises its outputs and its Diff.

## Tickets

| # | Ticket | Status |
|---|---|---|
| 01 | [Board Inputs and the Shaped Grid](issues/01-board-inputs-and-the-shaped-grid.md) | done |
| 02 | [A source held on a server answers its shape](issues/02-a-server-source-answers-its-shape.md) | done |
| 03 | [The Board reader](issues/03-the-board-reader.md) | done |
| 04 | [Drawing a Board](issues/04-drawing-a-board.md) | done |
| 05 | [Links narrow by values](issues/05-links-narrow-by-values.md) | done |
| 06 | [Outputs raised on a confirm](issues/06-outputs-raised-on-a-confirm.md) | done |
| 07 | [The Board Diff](issues/07-the-board-diff.md) | done |
| 08 | [The Chart Spec as a Board view](issues/08-the-chart-spec-as-a-board-view.md) | done |
| 09 | [The Board tool and the Docs Site](issues/09-the-board-tool-and-the-docs-site.md) | ready-for-agent |
| 10 | [ExBoard ships as packages](issues/10-exboard-ships-as-packages.md) | ready-for-agent |
| 11 | [A pivot's cell links](issues/11-a-pivot-cell-links.md) | done |
| 12 | [A Board over sources held on a server](issues/12-a-board-over-sources-held-on-a-server.md) | ready-for-agent |
