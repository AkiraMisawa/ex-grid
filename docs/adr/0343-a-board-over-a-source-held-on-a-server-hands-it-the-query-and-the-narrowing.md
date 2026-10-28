# A Board over a source held on a server hands it the query and the narrowing

*Taken in the user's absence, 2026-10-21, under their standing authorization (2026-10-20) for a fork
only they could settle: a reasonable default, recorded here, which they may overturn. ExBoard ticket 12.*

## Context

[ADR-0341](0341-a-board-input-declares-its-columns-and-a-source-answers-its-shape-first.md) binds an
input to a source, in memory or held where the data lives, and asks a source on a server only for what
is drawn. Ticket 02 gave such a source a call that answers its shape first
(`IShapedSource.ShapeAsync`). Until now, though, a Board bound only Snapshots: every view was built over
the whole of its input, and a link narrowed a Snapshot in memory
([ADR-0340](0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md), its
implementation notes on links). The vision this family serves names data too large to download, which
a Board over Snapshots cannot show.

Three things had no answer: how a grid built from a shape ([ADR-0342](0342-a-grid-builds-its-columns-at-run-time-from-a-sources-shape.md))
gets its rows from a server, how a link's condition reaches the server that executes it, and what a
view that cannot yet be drawn over a server source does.

## Decisions

- **A host binds an input to a `BoardSource`**, beside the Snapshots it binds others to. A `BoardSource`
  is the host's own code: it says the input's shape, fetches a grid's rows, and, if it can, makes a
  pivot's report source. The Board never talks to a server itself. Its host does, in whatever protocol
  the host already speaks, as the bundled fetching sources leave the protocol to their Consumer
  ([ADR-0025](0025-what-the-bundled-fetching-source-promises.md)). Binding is asynchronous
  (`BoundBoard.BindAsync`): each source's shape is asked and checked
  (`BoardInput.CheckAsync`) before any view is made, and a source that answers no shape is refused
  ([ADR-0341](0341-a-board-input-declares-its-columns-and-a-source-answers-its-shape-first.md)).
- **A grid's rows come back as a Snapshot page.** A grid view over a `BoardSource` is a Shaped Grid
  over a fetching source (`ShapedGrid.Fetch`). Each fetch hands the host the grid's query (its range,
  sort and filter, [ADR-0025](0025-what-the-bundled-fetching-source-promises.md)) and the link
  conditions narrowing the view now. The host answers with the rows as a Snapshot (an Arrow stream read
  with `SnapshotArrow`, for example), where they start, and the count. Each page is checked against the
  declaration as a whole Snapshot is. A page that lacks a column, or holds one of another kind, fails
  that fetch with the input named. It is never shown under the declared columns (spine rule 1).
- **A link hands the server its condition.** The condition is the same `LinkCondition` an in-memory
  Board executes: tuples over the link's columns, compared as the grid's value list compares them. It
  reaches the host with every fetch and every report source made for the view, and the host executes it
  where the data lives. When it changes, the view is asked again. A source that cannot execute a long
  list refuses with its reason, as ADR-0340 Q5 says. The Board does not cap the list.
- **A selection is read from the rows the grid holds.** A link or an output from a grid over a server
  source reads the values of the rows it covers from the pages fetched. A selection that reaches rows
  not fetched, such as a select-all of a million rows, narrows nothing and confirms nothing. The Board
  says so in the view's status line, and does not guess or partly read the selection. An output's
  version is the `Snapshot.Version` of the latest page fetched.
- **A date column with no format shows its time** over a fetched source. A shape says a column's kind,
  not whether its values hold a time of day, so the default is `yyyy-mm-dd hh:mm:ss`. The in-memory
  grid's `yyyy-mm-dd` would hide a time a later page held. A spec that writes `yyyy-mm-dd` shows dates
  only, as written.
- **Views that cannot be drawn over a server source yet are refused at binding, by path:**
  - **a chart**, until [ADR-0322](0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md)'s
    Chart Source exists. A chart reads every row it draws, and a series over a million rows needs the
    thinning done where the data lives;
  - **a pivot whose `BoardSource` makes no report source**;
  - **a link from a pivot over a server source.** The rows under a cell are on the server, so the
    values of the link's columns among them cannot be read in the client. A link whose columns are the
    pivot's own row and column fields could be read from the cells. It waits for a host that needs it.

  *Amended 2026-10-21:* the chart and the link are built. A chart over a server input is read through
  the source's `Thin` ([ADR-0344](0344-a-chart-source-answers-thinned-points-and-a-board-charts-a-server-input-through-it.md)),
  and a link from a pivot over one asks the source's `Under` for the values under its cells
  ([ADR-0345](0345-a-link-from-a-pivot-over-a-server-asks-the-source-for-the-values-under-its-cells.md)),
  never the cells' Items, which a subtotal does not name whole. Each is still refused at binding, by
  path, when the source answers no such question, as a pivot is without a report source.

## Consequences

- `ExGrid` gains `ShapedGrid.Fetch` and `ShapedPage`. A `ShapedRow` now knows its page, so one grid's
  rows may come from many Snapshots.
- `ExBoard` gains `BoardSource`, `BoardQuery` and `BoundBoard.BindAsync`. The component takes
  `Sources` beside `Data`.
- A Board is still the same JSON wherever its data lives (ADR-0340): nothing in it says which inputs are
  held on a server.
- ~~Open: the chart over a Chart Source, and a link from a server pivot. Each waits on its own decision.~~
  Settled by ADR-0344 and ADR-0345 (2026-10-21).

## Note of 2026-10-28: the Script Core's Board makes a Pivot Source, not a report source

`@ex-grid/board` binds a Board over sources too (`bindBoardThrough`, and `createBoard`'s `sources`), held to
the C# Board by the same tests, ported. One member differs in what it makes, because the Script Core's
pivot reads another kind of source. ADR-0370 section 3 has a server pivot answer Leaf Aggregates, with
the totals built in the page (`FetchingPivotSource`), and ADR-0151's report windows end with the C#
pivot. So the TypeScript `BoardSource.pivot(narrowing)` makes a `PivotSource` over the rows the
narrowing lets through, where C#'s `Report` makes a `PivotReportSource`. It is called again at each new
narrowing, and is refused by path when the source has none, as `Report` is. The rest of the decision
holds in both: the shape asked first, the narrowing handed to every question, `Unread`, the page's
version on an output, the date-time default, `thin` and `under` (ADR-0344, ADR-0345).

One thing is said that the C# Board leaves unsaid. A fetched grid whose source fails a question says
so under the grid ("The source did not answer the grid: …"), and keeps the rows it had, as a chart
whose source fails says so in its place.
