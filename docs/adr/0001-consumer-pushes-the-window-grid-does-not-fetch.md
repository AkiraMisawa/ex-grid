# The grid does not fetch. The Consumer pushes the Window in

The grid takes neither a collection of rows nor a function that fetches rows. It takes **the
Window that should be on screen right now** as a parameter, and when that is not enough it
**only notifies** that it needs a given range. What is displayed changes when the Consumer hands
over a new Window.

Sort state, filter state and whether a load is in flight are all owned by the Consumer and
passed in. **The grid neither sorts nor filters.**

There are two entry points. They are not peers: **the pull form sits on top of the push form**,
one-directionally.

```razor
@* Push — the bare entry point *@
<ExGrid Window="@State.Value.Window" TotalCount="@State.Value.Total"
        Sorts="@State.Value.Sorts"   Filter="@State.Value.Filter"
        IsLoading="@State.Value.IsLoading"
        OnRangeNeeded="r => Dispatcher.Dispatch(new RangeNeeded(r))"
        OnSortChanged="s => Dispatcher.Dispatch(new SortChanged(s))"
        OnEditsCommitted="e => Dispatcher.Dispatch(new EditsCommitted(e.Edits))" />

@* Pull — the bundled convenience layer. Internally it just drives the push form *@
<ExGrid Source="GridSource.From(_rows)" />        @* everything is in hand *@
<ExGrid Source="GridSource.Fetch(FetchAsync)" />  @* server paging *@
```

## How this was revised

Originally this ADR said the grid takes an asynchronous data source (a function) — the **pull**
form. The core of the decision has not changed (do not take a collection; provide from the start
a shape that can delegate sorting and filtering to a server), but **the direction was reversed**.
The reasons only surfaced after editing was added to the scope.

1. **Sorting on an overridden column breaks quietly in the pull form.** An edit is a sparse diff
   over the base ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)), which the
   server does not know about. In the pull form the grid holds the sort state and sends it in the
   `Query`, so **the grid cannot tell whether the order it got back is correct**. In the push
   form the grid does not sort, so it cannot produce a wrong order.
2. **Cache invalidation stops being a problem at all.** In the pull form the grid caches Windows,
   so something (a generation token) has to make it throw them away when the scenario changes. In
   the push form the grid holds no cache, so there is nothing to throw away.
3. **View State and Saved View become by-products.** Column widths, order, sort and filter must
   be serialisable for persistence (`CONTEXT.md`, **View State**). In the pull form that means
   two-way binding — lifting the grid's internal state out, and pushing it back on restore. In
   the push form the state is outside to begin with, so saving and restoring are just reading and
   writing it.
4. **It meets state-management libraries where they are.** Fluxor and the rest of the Flux family
   are push-shaped. Making push the native form removes the adapter.

## Considered Options

- **Take a collection of rows synchronously** — rejected from the start. It is the most pleasant
  to write against, and sorting and filtering stay synchronous and simple, but the day
  server-side data operations are needed it becomes a rewrite.
- **Make the pull form (an async function) native** — rejected. **It does not fail** — MudBlazor's
  `MudDataGrid` takes exactly this shape with `Items` / `ServerData` and is widely used. The
  grounds for rejection are the four points above, not that pull does not work. For a grid
  without editing, pull is enough.
- **Expose pull and push as peer public APIs** — rejected. It means maintaining two
  implementations, and how totals are produced and what "select all" means end up diverging
  between them. The two entry points here are **not peers**: `Source` is a thin layer that drives
  the push form internally, and there is only one behavioural path.

## Consequences

- **A Range Request names a non-empty range at non-negative positions** *(refined while
  implementing)*: a negative or empty range is refused at construction, so every Grid Source
  implementation faces the same, already-validated shape. A range **beyond the currently known
  rows is legal** — requests race with data updates — and answering nothing is the correct
  answer, as the reference implementation does.
- **The Consumer carries more.** Holding the Window, answering range requests and expressing the
  loading state are now its job. **The bundled `GridSource`** takes that over, so for a small
  Consumer the ergonomics are unchanged from pull
  (`<ExGrid Source="GridSource.From(_rows)" />`).
- **The bundled in-memory implementation is the reference implementation, and its semantics are
  the specification.** `GridSource.From` actually performs the filtering and sorting — which
  means it **decides the meaning** of things like whether `contains` is case-sensitive and
  whether nulls sort first or last. Server-side implementations must be written to match.
  [ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md) says the operator
  set becomes public API; operators alone are not enough, and the semantics are part of the
  specification too.
- **Forgetting to pass `IsLoading` breaks quietly.** While the user has changed the filter and
  the new Window has not arrived, the grid keeps displaying the stale result as if nothing
  happened. In the pull form the grid knew about its own outstanding request.
- **Scrolling responsiveness now depends on the Consumer's implementation quality.** Read-ahead,
  throttling requests during fast scrolling and discarding stale requests belong to the Consumer
  (or to `GridSource`).
- **Applying edits is not the grid's responsibility.** The grid paints the Window it is given and
  never applies an Overlay itself. What has to hold for an edit to reach the screen is in
  [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md), "the three links an edit
  passes through".
- **There has to be a cap on what can be copied to the clipboard.** The grid only holds the
  current Window, so it cannot copy a selection larger than that on its own. What happens beyond
  the cap is in [ADR-0005](./0005-copy-refuses-rather-than-truncates.md).
- **Inconsistency between pages does not arise for the first Consumer.** Its baseline is a
  content-addressed, version-stamped immutable snapshot, so pinning a query to a feed version
  keeps paging consistent. For a Consumer whose data streams continuously, that assumption does
  not hold.
