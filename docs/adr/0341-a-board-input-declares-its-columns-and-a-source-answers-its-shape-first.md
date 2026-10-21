# A Board Input declares its columns, and a source answers its shape first

*Decided with the user, 2026-10-20 (Q1 and Q2 of the grilling recorded in ADR-0340). The user added:
the data an AI obtains may come back to the client, or be obtained on the server and stay there, and
a huge result is never downloaded whole.*

## Context

A Board ([ADR-0340](0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md)) is saved and run many times, so a Data Handle, which lives for one
conversation, cannot name its data. And the data's types are not reliably known when the AI holds
it: a tool's result is usually JSON, which tells no decimal from a double, no integer, and no date.

## Decisions

- **A Board names its data by Board Inputs.** Each declares its name and each column's name and type
  (the Snapshot's column types, [ADR-0064](0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)). The AI writes them while it composes, when it knows
  what the data means.
- **An input binds to a source, not to a whole Snapshot.** In memory, or held where the data lives and
  asked only for what is drawn: a grid's Window ([ADR-0025](0025-what-the-bundled-fetching-source-promises.md)), a pivot's leaf aggregates and report
  windows ([ADR-0066](0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md), [ADR-0151](0151-server-pivots-send-report-windows-and-share-the-local-engine.md)), a chart's thinned pixels ([ADR-0322](0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md)). The engine chooses
  when it binds; the Board is the same either way.
- **A source answers its shape before any row**: its columns' names and types, a Snapshot its own, a
  result held on a server as an Arrow schema ([ADR-0065](0065-a-snapshot-travels-as-apache-arrow.md)). Binding checks the shape against the
  input and refuses a mismatch naming the input and the column (spine rule 1), so a change upstream is
  caught on the run that meets it, not drawn.
- **Types are never inferred from rows.** A first page of nulls, or `1` beside `1.5`, would be guessed
  wrong and drawn plausibly.
- **JSON becomes a Snapshot by the declared types.** Data that arrives as JSON is converted by the
  input's declaration; a value that does not convert (`"abc"` as a decimal) is refused by input, column
  and row, never dropped or read as zero.
- **No record type is generated at run time.** Reflection.Emit does not survive trimming and AOT on
  WebAssembly, and it is not needed: the Snapshot holds typed columns, and a view's row is a position in
  it.

## Consequences

- The declaration, the check and the JSON conversion live in `ExGrid.Data`, used by every product.
- A source held on a server gains a call that answers its shape; ADR-0322's Chart Source and the
  bundled grid and pivot sources each say it.

## Implementation notes

*A source answers its shape, 2026-10-21 (ExBoard ticket 02).*

- **One call, `IShapedSource.ShapeAsync`**, in `ExGrid.Data`, answers a `DataShape` or null for a
  source that was given none to say. `BoardInput.CheckAsync` asks it and checks the answer as
  `Check` checks a Snapshot's own; a null is refused, since kinds are never inferred from rows.
- **The bundled fetching grid source and the fetching report source take a `Shape` call** that asks
  the server, set before the source is bound: a property, not a parameter of their factories, whose
  shipped signatures stay as they are. A report source over a Snapshot, records' included, answers
  the Snapshot's own; any other `PivotReportSource` answers none unless it says otherwise.
- **An Arrow stream's schema answers it** (`SnapshotArrow.ReadShapeAsync`): the first message alone,
  by ADR-0065's table, the stream left at its first record batch. An Arrow file, whose schema is in its
  footer after its rows, is refused rather than read whole.
- **ADR-0322's Chart Source is not built yet**, so it gains the call when it is. A chart in a Board
  reads Snapshots today, whose shape is their own.

*A Board bound to a source held on a server, 2026-10-21 (ExBoard ticket 12).* How a bound source is
asked for rows, and how a link's narrowing reaches it, is [ADR-0343](0343-a-board-over-a-source-held-on-a-server-hands-it-the-query-and-the-narrowing.md).
