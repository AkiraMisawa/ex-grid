# ExPivot asks a Pivot Source, and a server answers with the Leaf Aggregates

*(Decided with the user, 2026-09-30, in the ExPivot grilling — Q2, Q12 to Q15, Q21, Q23 to Q25,
Q28, Q36, Q40, Q50 and Q57. It replaces [ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)'s
"aggregation runs in ExPivot, in process, over the snapshot", and settles the pivot query that
ADR-0058 reserved. The user started it: "a million-row CSV is normal; I want a model where a server
aggregates and filters — isn't that what ExGrid was designed for?")*

ExGrid does not fetch. The Consumer pushes, and a server that answers for it is held to
`GridSource.From` ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md),
[ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)). **ExPivot takes
the same shape.**

- ExPivot asks a **Pivot Source** for three things: what a report is computed from, a field's
  Items, and the records behind a cell.
- The bundled source computes these in process, from a Snapshot, and it is the reference
  implementation.
- A Consumer's server may answer instead, and is held to the bundled source's answers.

The data may be in the browser, in a Blazor Server host's process, or in a database, depending on
the application (Q21); every one of those places is served by this one shape.

```
ExPivot ── PivotQuery ────────► Pivot Source ─┬─ PivotSource.From(snapshot)   in the process: WebAssembly, or a Blazor Server host
        ◄── Leaf Aggregates ───               └─ PivotSource.Fetch(…)         the Consumer's transport to a server, which answers
                                                                              from a Snapshot (the same engine) or from SQL
```

## What a server answers with: the Leaf Aggregates

Three shapes were weighed (Q12):

- **The Leaf Aggregates — chosen.** The answer has one leaf for every combination of the row and
  column fields' Items that has records once the Hidden Items are left out. Each leaf carries the
  parts each Value Field's Aggregation is computed from, **and only the parts the Value Fields ask
  for**: the count of values, the count of numbers, the sum, the extremes, the product, the running
  variance. ExPivot computes every cell, subtotal and grand total from these parts, and lays out
  the form, sorts, collapses and applies Show Values As, all without asking again. In SQL it is one
  `GROUP BY`.
- **The finished report, a window of its rows** — rejected. The server would have to implement
  every form, subtotal rule, sort by value and collapse to Excel's rules, and every collapse would
  be a round trip.
- **The records, in pages** — rejected. At a million records, that is the problem, not the answer.

**The parts combine exactly.** A subtotal's sum is the sum of its leaves' sums, its count is the sum
of their counts, and its variance is their running variances merged. **A total is therefore still
computed from its records, never from the totals below it**
([ADR-0059](./0059-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)): an
Average's grand total is the grand sum over the grand count, not an average of averages.

A report a person reads has far fewer leaves than the data has records. The measured report over a
million records had 2,976 leaves, and its aggregates took about 1 MB.

## The contract

```csharp
public abstract class PivotSource
{
    IReadOnlyList<PivotField> Fields { get; }        // what the Field List offers
    PivotSourceFeatures Features { get; }            // the Aggregations it answers; whether it can be refreshed
    ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken ct);
    ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken ct);
    ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken ct);
    ValueTask RefreshAsync(CancellationToken ct);    // Excel's Refresh
    event Action<PivotSourceChanged> Changed;        // the data moved on: ask again
}

PivotQuery        = the row fields and the column fields, in order;
                    the Hidden Items of every placed field;
                    for each field in Values, the parts asked for;
                    MaxLeaves
PivotAnswer       = its Source Version;
                    each row and column field's Items;
                    the leaves: each one's Items, and its parts
                  | a refusal: more leaves than MaxLeaves, an unknown field, or an Aggregation not offered
PivotItemsQuery   = a field, a search, how many, under a Source Version
                    → that field's Items over all the data, with how many there are
PivotDetailsQuery = a cell's Items, the Hidden Items, a range, under a Source Version
                    → one page of the records behind the cell, with their total
```

- **Every question and every answer is a serialisable value**
  ([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md)). Each has a JSON
  form, `PivotJson`, that carries its own version, so it crosses to a server unchanged. A document
  of a version the reader does not know is refused.
- **`PivotSource.From(snapshot)` answers from a Snapshot**, using `ExPivot.Engine` (ADR-0059). It is
  the reference.
  - **A server that holds a Snapshot answers with this same source**, so its answers are the
    reference's by construction.
  - **A server that answers from SQL is held to the reference by tests** that ask both the same
    questions.
- **`PivotSource.From(records, fields)` is the short path for records in memory.** Typed field
  declarations (Q53) build the Snapshot and declare the fields in one go.
- **`PivotSource.Fetch(fields, features, aggregate, items, details)` takes the Consumer's transport
  as three delegates**, as `GridSource.Fetch` does
  ([ADR-0025](./0025-what-the-bundled-fetching-source-promises.md)) (Q50).
  - The pivot never opens a connection.
  - Authentication, retries and the transport itself are the Consumer's.
  - `ExPivot` ships no HTTP client and no server endpoint.

## Source Version: an answer says what data it came from

A server's data can move on between the report being computed and a user opening the records behind
a cell. The records would then no longer sum to the cell (Q23).

- **Every answer carries its Source Version.**
- **A field's Items and the records behind a cell are asked for under that version.**
- **A source that can no longer answer under that version refuses.** ExPivot then says "the data
  has changed — refresh" rather than show records that do not add up.
- **A server needs only a change counter or a timestamp to compare against**, and keeps nothing
  else.
- **The bundled source answers under any version it still holds.** A Snapshot is immutable, so its
  records always add up.

## A source says what it cannot do

Any SQL database answers Sum, Count, Min, Max and Average with one `GROUP BY`. It may not answer an
exact Product or variance (Q24).

- **A source declares the Aggregations it answers.**
- **Value Field Settings… offers the others disabled, with the reason**, as it disables any command
  that would change nothing
  ([ADR-0060](./0060-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
- **A source is never obliged to approximate an Aggregation.**

## Asking never blocks, and a stale answer is never painted

Over a million records, a new aggregation with the first engine took 0.7–1.6 s on CoreCLR and
7–11 s in the browser, with the page frozen throughout (Q14).

- **Every question is asked asynchronously.** While it is out:
  - the Field List already shows the new layout;
  - the report stays as it was, under a loading indication;
  - a further change **cancels the question in flight** and asks the next one.
- **An answer to a question that is no longer the current one is always discarded.** This is the
  rule ADR-0025 gives the grid.
- **The bundled source works in slices and yields between them**, so a browser keeps painting. A
  slice is sized to stay within 50 ms (the Definition of Done's observational targets).
- **Defer Layout Update** — Excel's checkbox at the foot of the pane, with its Update button —
  holds the Field List's changes, so a user building a large report asks only once.
- **A change that needs no new question asks none.**
  - Collapse, order, the form, subtotals, grand totals, Show Values As, captions and formats are
    laid out from the answer already held.
  - When a Value Field's Aggregation changes, ExPivot asks again only if the answer it holds lacks
    the parts the new Aggregation needs.

## Caps: a report too large to read is refused by name

A report's cost grows with its cells, not its records. With TradeDate's 270 dates in Columns, the
same million records made 533,881 leaves and took 258 MB (Q15, Q28). **Principle 5 puts the cap on
what cannot be executed:**

- **The leaves.** A question carries `MaxLeaves`, and a source that would need more leaves refuses.
- **The report's rows and columns.** The caps are Excel's own limits: 1,048,576 rows and 16,384
  columns.
- **A layout that breaks a cap is refused by name**, for example "this layout needs more than
  200,000 cells". The report stays on the layout it had before.
- **Each cap has a default, and the Consumer may change it** (Q28). The right cap depends on the
  device, and on where the aggregation runs.
  - The default for the leaves is 200,000. It is provisional, and the Definition of Done's
    observational targets record the measurement that settles it.

## Refresh

A source that can be asked again, such as a server's, says so in its features (Q25).

- **The report's toolbar then offers Refresh**, and the Consumer can refresh from code as well.
- **The bundled source shows no button.** It is refreshed by handing ExPivot a new source, or a
  Change Batch
  ([ADR-0066](./0066-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).

## Not in the first version

- **A source that writes its own SQL**, that is, a `GROUP BY` built from a `PivotQuery` for each
  SQL dialect (Q40). When it comes, Entity Framework Core's LINQ is the natural way round the
  dialects, in a package of its own (`ExPivot.EntityFrameworkCore`). Until then, the demo's server
  shows the SQL written by hand.
- **Engines such as DuckDB behind a source** are documented, not bundled (Q36), because they bring
  native libraries.

## Refined while building it

*(2026-10-01, when the contract was built.)*

- **A field's Items come in no promised order, and a source's search matches an Item's invariant
  text, ignoring case.** A source knows neither the report's culture nor its formats, so it cannot
  order or search painted labels. Filter… orders the Items itself, as it orders any field's, and
  narrows the painted labels among the Items it holds. It asks the source with the typed search
  only when a field has more Items than it lists (10,000, ADR-0060).
- **A field in Filters that hides nothing does not travel in a question.** It changes no leaf, so
  placing it, or moving it while it hides nothing, asks no new question. Only the row fields, the
  column fields and the Filters fields with Hidden Items are part of what the answer was computed
  from.
- **An answer may carry more parts than were asked for**, and ExPivot uses what it is given.
  `PivotSource.Fetch` refuses on its own, without asking the server, a question that names an
  unknown field or asks for a part no Aggregation it offers reads. It refuses an answer to a
  different question than the one asked.
- **The bundled source's Source Version is new for each source**, so handing ExPivot a new source
  is a refresh that no older question survives.
- **`PivotJson` is written by hand**, not through `JsonSerializer`, so trimming a browser
  application cannot break it. Each document names its format version and its type. In a sum or
  an extreme, a JSON number is an exact decimal and a string is a `double`, so `0.1 + 0.2` travels
  as the `double` it is, and a sum of money as the decimal it is.
- **Counts are 64-bit throughout**, because a server's data is not bounded by a process's.

- **While a new version's Items are on their way, the previous version's stay in view.** With a
  server's source, every redraw of live data brings a new Source Version. Re-listing would make
  the report filter band read "Loading…" and disable Filter…'s OK for a round trip after each
  redraw. Hidden Items are keys, which name the same Items under any version, so ticking and
  applying against the Items in view is safe. The Items of the new version replace them when they
  land.

## Consequences

- **ExPivot's entry point is now `PivotSource.From`, not `PivotEngine`.** The engine is what the
  bundled source runs.
- **Layer 1 holds the bundled source to the engine's rules, and the demo's SQL source to the bundled
  source**, question for question, over the same data (§29).
- **ADR-0058's "reserved" row becomes part of the first version**, and its "Who owns what" is
  rewritten.
- **The component gains state for asking**: a loading indication, a question to cancel, and an
  answer to discard. ExGrid's `IsLoading` paints the indication
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).
