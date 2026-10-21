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

## Implementation notes

*Links, 2026-10-21 (ExBoard ticket 05).* Defaults taken where the decision left a choice, recorded
here so they are not re-derived:

- **The condition is the selected rows' tuples**, one per distinct combination of values in the
  link's `on` columns, never a value list per column. Per-column lists would let (Rates, Feb) through
  a selection of (Rates, Jan) and (Credit, Feb): plausible and wrong (spine rule 1). On one column it
  is exactly ADR-0002's value list filter; on several, ADR-0002's model, whose columns combine with
  And, cannot say it, so the condition is a model of its own (`LinkCondition`), its values compared
  as the grid's value list compares them (text without regard to case, every other value by its
  type, a Blank only to a Blank).
- **In memory, ExBoard executes it** over the bound Snapshots: a grid's rows are handed again to its
  source, which keeps the user's sort and filter; a pivot is given a source over a Snapshot of the
  rows let through; a chart reads only those rows. A source held where the data lives (ticket 02) is
  handed the condition instead. A pivot's narrowed Snapshot is a copy, made once per change of
  condition (a selection moving within the same values narrows nothing again), so its cost is the
  input's size; a large input belongs on a server, where the copy is not made.
- **A link starts at a grid or a pivot** (a pivot since ticket 11, below). A chart's selection does
  not narrow.
- **Links run one way**: a link that would let a view narrow, directly or along other links, the
  view that narrows it is refused. Such a view would drop its own selection each time it was
  narrowed. Along a chain, a narrowing that changes a grid's rows drops that grid's selection
  (ADR-0011), and the views it narrowed are restored with it.
- **The selection is read against the rows as the grid shows them** when it is raised; a range past
  them narrows nothing.

*Outputs, 2026-10-21 (ExBoard ticket 06).*

- **An output's `from` names a grid view**, as a link's does: the Decision's example
  `{"from": "trades", …}` is read as a view's id, and the output is on the input that grid reads.
  An output stays a grid's after ticket 11: it hands on rows the user chose, and a pivot's cell is
  many rows the user did not see one by one.
- **The confirm is the output's own `confirm`, its label**, drawn under its grid. It is never
  disabled: a press that follows a selection a round trip later is answered for that selection, where
  a button the browser still held disabled would drop it (spine rule 6). With nothing selected it says
  so. An output with no confirm is refused: nothing raises it but the user.
- **The confirm reads the selection from its grid, not from the last `SelectionChanged`.** A grid
  raises its selection after the render that paints it, which on the Server host is a round trip after
  the press, so a confirm pressed straight after a click found the selection before it (seen in CI's
  repeat of the confirm test on the Server host). The confirm asks the grid for its selection once the
  grid has answered every input it received before the press (`ExGrid.SelectionAfterInputsAsync`, in
  the grid's Input Order, ADR-0240), so the press acts on the selection it was made against, at any
  speed (spine rule 6). The Board is not given an Input Root of its own: each grid keeps its order,
  and a confirm outside them waits on the one it reads.
- **The condition is a link's** (`LinkCondition`, the selected rows' tuples), and the version is the
  bound Snapshot's `Version`. `BoardOutput.Take` refuses data of another version, and other data at
  version 0, which names no version and so cannot vouch for itself; the very data it was taken from is
  always taken. A source whose data moves on gives each version its number. It refuses a column of
  another kind too, which would otherwise match nothing, quietly.
- **A grid narrowed by a link lets go of its selection's output and links at once**, as it drops the
  selection (ADR-0011), not when it next raises the dropped selection.
- **Its JSON** names the output, the input, the version, the columns and each tuple, a date as
  `yyyy-MM-dd` (with `THH:mm:ss` when it holds a time), so an engine in any language can run it.

*The Board Diff, 2026-10-21 (ExBoard ticket 07).*

- **A part is a view's, and only where it differs from the Board.** A grid's part is its View State
  (ADR-0207) and the columns the user hid; a pivot's is its layout; a chart's its hidden series, range
  and axis (ADR-0272). A view the user has not changed, or has changed back, has none. The JSON is
  `{"version": 1, "views": {"<id>": {"grid" | "pivot" | "chart": …}}}`, each part in its own reader's
  form, so the View State and the layout keep their versions.
- **A grid's part names the columns hidden as well as those shown.** The View State leaves a hidden
  column out, which next time would hide a column the Board added since as well; named, a column in
  neither list is one the Board added, and is shown, last.
- **Fitting drops by the smallest part that no longer fits.** A view gone, or now of another kind,
  drops its part; within a grid, a column, a sort level or a column's filter on a column the grid no
  longer shows is dropped alone and the rest applies; a pivot layout placing a field its input no
  longer declares, or declares as another kind, is dropped whole, as a layout without one of its fields
  is another layout. A part names the kinds of the columns it depends on, a grid's filtered columns and
  a pivot's placed fields, so a filter set on a column declared another kind since is dropped, never
  laid over a column its operands no longer read (spine rule 1). A chart's part is not checked against
  its spec: a hidden key naming no series, or a range holding no point, is said on the chart
  (ADR-0272). A hidden column the grid no longer shows is named among the drops too. Each drop
  is one sentence naming its path, in `BoundBoard.Dropped`, and the component says them above the
  views. A Diff that is not one this reader reads (another version, a member it does not have) is
  refused whole, as a Board is: that is not a Board that has changed, but text that is not a Diff.
- **The user's changes are applied in the Board, not in the spec.** The grid's gestures (width, order,
  hide, pin; ADR-0010/0016) are taken by the bound view and pushed back as its Columns, so a Board's
  grid now offers them; its sort and filter stay its source's. The last column shown cannot be
  hidden, since a Board offers no way to show one again.
- **Raised only on a change.** A narrowing moves a grid's rows, not its View State, and raises
  nothing; a Diff laid over is the starting point, not a change, unless some of it was dropped: then
  the Diff that applied is raised, so a host keeping each one raised keeps no part that will not apply.
  The component reads `Diff` when it changes, as it reads `Data`, and compares by the JSON, so a host
  that hands back each Diff it is raised binds nothing again. A Board bound again for new data or a new
  Board keeps what the user has changed since; only a new Diff replaces it.
- **A gesture behind the columns is refused.** A resize, an order or a pin naming a column no longer
  shown, a round trip behind a hide on the Server host, is not taken (spine rule 6).

*A pivot's cell, 2026-10-21 (ExBoard ticket 11).*

- **ExPivot says what a selected cell stands for** (`ExPivot.ItemsOf`, `PivotCellItems`): its row's
  path and, for a value cell, its column's, as Show Details reads them. A selection reaching a row the
  report does not hold drawn is read as nothing (null), not as the part it could read (spine rule 1);
  the Board says so under the pivot and narrows nothing. In the outline and tabular forms a label
  column of an outer field stands for that field's Item, the path down to it, not for the row's leaf.
  It is read when the grid raises the selection, against the rows the grid holds then: ExPivot hands
  its grid a new report's rows in the render that changes its own, and the grid drops a selection
  whose rows changed (ADR-0011), so the two never disagree when a selection is raised.
- **The rows under a cell are matched in the Board**, not through Show Details' query: a Board's
  pivot reads its input's columns as they are, with no alias or date part, so a cell's Item is the
  column's value keyed as the pivot keys it, and the hidden Items are the layout's the pivot is drawn
  with. A Board that gains derived fields moves this onto the engine's query.
- **The condition is the tuples of the rows under the cells**, read from the rows the pivot shows, in
  the link's `on` fields: a row is under a cell when its value in each field the cell's paths name is
  that Item (`PivotItemKey.For`, as the pivot groups it). A row whose value is an Item the pivot hides
  is under none, its grand total included. So a cell constrains only the fields its paths name, and
  the condition keeps ticket 05's form, tuples, whatever field the link compares.
- **A pivot's link compares only fields it places in its rows or columns**; one placed as a value or
  a filter, or not placed, is refused by path, since no cell names it.
- **A pivot narrowed again, or given a new layout, lets go of what its cells narrowed at once**, as a
  grid does: its report is computed again and its selection dropped (ADR-0011).

*The Chart Spec as a view, 2026-10-21 (ExBoard ticket 08).*

- **A chart view is read against its inputs' declarations when the Board is read**, by the Chart
  Spec's own reader over tables of no rows that hold the declared columns. A series on a column its
  input does not declare, or anything else the reader refuses, is refused then, by its path under the
  view, not when data is first bound. A column's kind is not checked there: the reader checks kinds by
  the values it reads, and binding checks the source's shape against the declaration (ADR-0341), so a
  kind can only be wrong in data that binding has already refused.
- **`draw_chart` stays as it is**, naming Data Handles. `BoardSpec.OfChart` makes its spec a Board of
  one view, `chart`: each handle the spec names, and only those, becomes an input declared with the
  shape of the table the host holds for it, and a refusal keeps the spec's own path, as the tool says
  it. The tool's answer is still `ChartSpec.Answer`; the Board is how a host draws it beside the rest.

*The tool, 2026-10-21 (ExBoard ticket 09).* What Consequences left to the implementation:

- **`draw_board` takes the Board and the Data Handle each input is bound to now**,
  `{"board": {…}, "data": {"trades": "h-7f3a"}}`. The handles stay out of the Board, which is what is
  saved; the call binds them for this conversation alone. Every declared input is bound, and a member
  of `data` naming an input the Board does not declare, or a handle the host does not know, is
  refused by its path under `data`; a table whose shape is not its input's declaration is refused
  there too, naming the input and the column.
- **What the AI is told of a source is its declaration**: `BoardSpec.DeclarationOf(shape)` writes the
  shape as an input's `{"columns": […]}`, so the AI copies the types rather than reading them off JSON
  rows (ADR-0341).
- **An accepted Board is said in words**, as ADR-0320's answer is: its title and how many views and
  rows; each view in the layout's order, a grid by its rows, input and headers, a pivot by its rows,
  input, placed fields and values, a chart by its own summary (ADR-0275); then each link and output.
  A refusal is one sentence, prefixed by its path (`board.views[0].grid.columns[2].format: …`).
- **The answer carries the Board read and the Board bound**, so a host draws what it answered without
  reading the call twice.
