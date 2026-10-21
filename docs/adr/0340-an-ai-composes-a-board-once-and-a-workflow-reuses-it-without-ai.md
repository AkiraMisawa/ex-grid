# An AI composes a Board once, and a workflow reuses it without AI

*Decided with the user, 2026-10-20, in the grilling of the family as an AI's visualisation tool
(Q1 and Q4 to Q8). It began from the user's question of whether the Chart Spec's approach
([ADR-0320](0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md)) should reach the grid, the pivot and the sheet, and from the use the user has in mind.*

## Context

The user's picture: a user needs a workflow the web application does not have yet. An AI composes it
once, from natural language, out of primitive steps. From then on the workflow is fixed and runs
without AI, again and again. Its engine is the host's, written in house or taken from an open source
project. The family is the step that shows what the workflow obtained, and what that data looks like
is not known until the AI has it: which columns, which types, a grid, a pivot or a chart. Until now,
showing data of a shape not known at compile time took code written for that shape.

A Chart Spec answered this for one chart. It named data by a **Data Handle** that lives only for the
conversation, it could not be laid beside a grid or a pivot, and nothing the user did in it reached the
next step.

## Decisions

- **The Board.** A Board is JSON that lays out views of the family (grids, pivots and charts) in rows
  and columns, links them, and declares what it hands on. An AI writes it; it is saved; a workflow
  runs it with no AI. The layout is a plain row and column arrangement, never free placement, and the
  family never chooses one: where each view goes is written in the Board.
- **A Board names its data by typed named inputs** ([ADR-0341](0341-a-board-input-declares-its-columns-and-a-source-answers-its-shape-first.md)). The workflow binds each input to a
  source at run time, in memory or held where the data lives, and the Board is the same either way.
- **A Chart Spec is one view of a Board** *(rewrites ADR-0320's Data Handle, Q4)*. Its `data` names a
  Board input, under the Board's declarations and checks. `draw_chart` stays, as a Board of one view.
- **A link narrows by values** (Q5). A link names columns (`"on": ["desk", "month"]`); a selection in
  its view, a pivot's cell or a grid's rows, becomes an equality filter on those columns, which the
  narrowed view's source executes as any filter ([ADR-0002](0002-filters-are-a-serializable-model-not-linq-expressions.md)). It works across inputs that share the
  column names and over data held on a server, and keeps its meaning when the data moves on. Not Row
  Keys: a pivot cell stands for many rows, and a key list would cross the wire. When the selection in
  the linking view is dropped ([ADR-0011](0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)), its filter goes with it; a stale filter never stays
  quietly. Selection is not capped (spine rule 5): a source that cannot execute a long list of values
  refuses with the reason.
- **A Board's output is a value condition, raised on a confirm** (Q6). An output is declared
  (`"outputs": {"chosen": {"from": "trades", "on": ["tradeId"]}}`) and raised only by a confirm action
  the Board declares, never by a selection on the way. It carries the condition on the input and the
  version of the source it was taken against; the engine executes it where the data lives, or makes a
  Snapshot of it, and refuses a version that has moved on (spine rule 6). It is raised as a .NET event
  and as JSON, so any engine can take it.
- **Running a Board never rewrites it** (Q7). What the user changes, a sort, a width, a pivot's layout,
  a chart's range, is raised as one **Board Diff**: the grid's View State JSON ([ADR-0207](0207-the-view-state-travels-as-json-through-an-entry-point-of-exgrids-own-each-operand-with-its-type.md)), the
  pivot's layout JSON, the chart's raised changes ([ADR-0272](0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md)). The host may keep it per user and
  lay it over the Board next time (spine rule 2). A part that no longer fits, a column gone, is dropped
  by name and the rest applies. Making it the default is the host or the AI writing a new Board.
- **Where it lives** (Q8). An input's declaration and its check are `ExGrid.Data`'s, beside the
  Snapshot, since every product uses them. Each product reads its own view: the grid in `ExGrid`
  ([ADR-0342](0342-a-grid-builds-its-columns-at-run-time-from-a-sources-shape.md)), the pivot in `ExPivot.Engine`, the chart in `ExChart.Engine`, so each can still be
  drawn from a spec alone. The Board, its layout, links, outputs, Diff and tool definition, is a new
  package, `ExBoard`, referencing the three, with an `ExBoard.MudBlazor` Wrapper ([ADR-0019](0019-one-repository-many-packages.md)). No
  Engine gains a reference. *The user chose the name:* `Ex` claims the operability of the views it
  lays out; the nearest thing in Excel is a sheet of PivotTables and charts narrowed by slicers.
- **The family holds no workflow.** It reads a Board, checks its inputs, draws it, and raises its
  outputs and its Diff. It runs no step, holds no data and stores no Board (spine rule 3).
- **Refusal speaks as the Chart Spec's does**: one sentence naming the path and the reason, with a
  required `version`, an unknown one refused.

## Consequences

- The Board's JSON is a public surface that saved workflows depend on for years; a change to its
  schema is a Change Note ([ADR-0190](0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)), and a version is read, never guessed.
- A pivot read from a spec answers as Excel's PivotTable, and the text returned to the AI is the
  aggregates themselves, so the AI does not compute numbers it then shows.
- The sheet is not a view of a Board. What an AI does to a sheet is an edit through the one undo stack
  ([ADR-0048](0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)), to be decided on its own.
- What an AI is told while it composes (a source's shape without its rows, a Board said in words) and
  the tool definitions follow ADR-0320's form and are settled with the implementation.
- The grilling's record is `exchart/grilling-family-ai.md` in the project's shared files.
