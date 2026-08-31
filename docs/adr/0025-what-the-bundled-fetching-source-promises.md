# The bundled fetching Source breaks its own cold start, and never lets a stale answer through

[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) settled that the grid
does not fetch, and handed three things to "the Consumer (or to `GridSource`)":
**read-ahead, throttling requests during fast scrolling, and discarding stale requests**.
It never said what the bundled one does. `GridSource.Fetch` is that implementation, so
this is where it is decided.

The shape:

```csharp
GridSource.Fetch<TRow>(Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
                       int readAheadRows = 60)

GridQuery      = (RowRange Range, IReadOnlyList<SortSpec> Sorts, GridFilter? Filter)
GridPage<TRow> = (IReadOnlyList<TRow> Rows, int Start, int TotalCount)
```

`GridQuery` is the glossary's **Query** made concrete: a serialisable model, not a
delegate ([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md)), so
it crosses to a server unchanged. What its Filter and Sort *mean* is not this source's to
decide — `GridSource.From` is the reference implementation and a server is written to
match it ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).

## The source fetches the first page itself

**This is the decision that shapes the rest.** The grid asks for the rows that are
visible; with an empty result nothing is visible, and ADR-0001 already settled that
**"nothing to show is not a range"** — a total of zero asks for nothing, and an empty
Range Request is refused at construction. A source that starts with no rows therefore
cannot be woken by the grid at all.

```
grid:   0 rows → nothing visible → no Range Request        ← correct, and pinned by a test
source: no rows until someone asks                          ← deadlock
```

So the source breaks the cold start: **the first column push is bind time**, and bind time
is when the first page is worth fetching. The same applies whenever a Sort or Filter change
empties the Window (below).

Rejected: **letting the grid ask for a first Viewport when it has nothing**. It would
overturn a decision already made and tested — with an explicit `TotalCount` of zero, the
Consumer has *said* the result is empty, and asking anyway is the grid disbelieving it.

## What it does with a Range Request

| | |
|---|---|
| The Window already covers it | nothing. The grid asks about the Viewport; the source answers about what it holds |
| Otherwise | widen by `readAheadRows` either side, clamp to the known total, and fetch |
| The widened range equals the one in flight | **hand back the fetch already running.** Awaiting it is also what carries a failure to the grid |
| A different range while one is in flight | cancel the old one and start the new. **The cancel is a courtesy to the server; the generation counter is what makes it correct** |
| An answer whose generation is not the current one | **discarded, always.** This is the accident ADR-0001 named: a fling asks three times, and the first answer arriving last would paint rows nobody is looking at |
| An answer that does not reach **the range the grid needed** | **refused by name.** A source may answer with fewer rows than the read-ahead asked for, or with none when the result has shrunk past that position — but the rows the grid is about to paint have to be in it, or the Viewport stays on Placeholders, the grid asks again, gets the same answer, and its own dedupe then goes quiet: no rows, no load, no error. *(The check is against the **unwidened** range. Judging it by the widened one accepts a page that reaches none of the rows the grid needs — a server capping its pages below the read-ahead window does exactly that.)* |

Read-ahead is a number the Consumer passes rather than a guess in the grid: how many rows
are worth fetching ahead is a judgement about their data and their server, which is exactly
why ADR-0001 declined to make it the grid's.

## A Sort or Filter change starts again from the top

Everything the source holds is invalidated at once — the rows, their order, and the total,
which the new query has not counted. So: **Row Sequence Version is bumped** (the selection
is dropped, [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
the Window is dropped rather than left standing, and the first page is fetched from the top.

Keeping the old rows visible under a new filter would be the "quietly wrong" failure this
whole component is built to refuse: they are not the result, and nothing on screen would
say so. `IsLoading` covers the gap (ADR-0010's loading seam), which is why ADR-0001 warns
that forgetting to pass it breaks quietly.

**Scrolling to another slice is not a reorder** and does not bump the version: dropping the
selection every time the Window moved would make a long cross-page selection impossible to
build, which ADR-0015 explicitly wants to work.

## A failure is never silent

| | |
|---|---|
| `IsLoading` | goes back off |
| The Window | **is left exactly as it was.** Removing rows over a network hiccup empties a screen that was fine a moment ago |
| `LastError` | holds the exception, for a Consumer that would rather read a property than subscribe |
| `FetchFailed` | raised |
| No subscriber | **the exception is rethrown** — on the grid's awaited call when the grid asked, and otherwise on the synchronization context the source was created on, so it reaches the host's error UI. **A source built where there is no context** (a DI service, a static initialiser) has nowhere to rethrow to: the task is left faulted rather than swallowed, and `LastError` holds it — but such a source **must subscribe to `FetchFailed`**, or a failed cold start is a blank grid and a silence |

Rethrowing when nobody is listening is the point. A failed fetch that vanished leaves the
previous rows on screen looking current, and a Consumer who never wired up an error handler
is exactly the one who would not notice.

## Two things that only look like details

**A query change reports itself whatever the loading flag was doing.** The Window, the total
and the Row Sequence Version all move at that moment, and none of it depends on whether a
fetch happened to be in flight — which, while the user is scrolling, it usually is. Reporting
only the `IsLoading` flip leaves a filter change invisible in exactly that case: the old rows
stay on screen under the new filter, carrying a selection ADR-0011 says must be dropped.

**A result that shrank drops the selection too.** Positions are what a selection is made of
(ADR-0011), and when the total comes back smaller they name different rows, or none. Growing
is safe — the existing positions still mean what they meant.

## Consequences

- **A failed range is not marked as answered, but nothing retries it on its own.** The grid
  asks once and not again until the Window moves (ADR-0001), so after a failure the retry
  comes with the next scroll. A source that should retry by itself — a backoff, a "try
  again" button — is the Consumer's to build on top; `FetchFailed` is where it hooks in.
- **The Window is one slice.** Nothing is cached: scrolling back to a range that has
  already been fetched fetches it again. Caching means deciding when to throw a cached
  Window away, which is the invalidation problem ADR-0001 got rid of by making the grid
  hold nothing. If a measurement later shows the refetch is what hurts, the cache belongs
  here, in the source, and not in the grid.
- **The Consumer disposes the Source.** The grid does not own it — it is handed one, and
  several grids could in principle be handed the same one — so `FetchingGridSource` is
  `IDisposable` and the Consumer that built it releases it. Left undisposed, a fetch
  outstanding at teardown goes on running and goes on raising `StateChanged`.
- **`PageSize` and a pager are not here** ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)).
  Paging changes only what drives a Range Request, so it rides this same machinery when it
  comes.
- **Sort and Filter are on the concrete source, not on `IGridSource`.** The grid has no
  gesture that changes either yet — no header click, no column menu (ADR-0010) — so the
  interface would be publishing a method nothing calls. The notification joins the
  interface with the code that raises it.
