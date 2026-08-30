# Filters are a serialisable structured model, not LINQ expressions

The filter the grid hands over is a **structured model** of `{column, operator, value}` combined
with AND/OR. `Expression<Func<TRow, bool>>` is not used. Sort is likewise a list of
`{column, ascending/descending}`.

Two reasons. **An expression tree cannot cross HTTP** — it works perfectly against an in-memory
implementation and then, the moment the implementation is swapped for a server-side one, it
cannot be turned into JSON, and the swappable seam that
[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) exists to provide loses its
point. And **with Excel-like operability the grid renders the filter UI itself**, which requires
the grid to understand what a filter means.

## Confirmed against a shipped library

MudBlazor's `MudDataGrid` takes the pull shape and exposes `FilterDefinitions` / `SortDefinitions`
to the `ServerData` callback. Users hit exactly the failure predicted here: the filter and sort
delegates the grid produces are built for client-side LINQ-to-Objects evaluation, and using them
against an Entity Framework queryable fails to translate. The discussion recording this is
unanswered. The same source notes that for template columns the Consumer has to walk
`RenderedColumns` to map column GUIDs back to property names — the Consumer reaching into grid
internals, which is what happens when the grid owns state the Consumer needs to interpret.

## Considered Options

- **`Expression<Func<TRow,bool>>` (LINQ)** — rejected. It is the first thing a .NET developer
  reaches for, is type-safe, and is natural against an in-memory implementation. It is not
  serialisable.
- **Pass through an opaque Consumer-defined object** — rejected as the primary form, though
  partially adopted as the escape hatch below. It is the most flexible, but the grid cannot render
  filter UI for something whose contents it does not know, so the Consumer ends up building the
  filter screen. That pushes the core of Excel-like operability onto the Consumer.

## Consequences

- **There is an escape hatch alongside, for conditions the model cannot express.** An **Opaque
  Filter** sits next to the structured filter. The grid renders UI only for the former and passes
  the latter through untouched. It exists for conditions that no general model should carry.
- **The Consumer needs to answer "give me the distinct values of this column".** Excel's filter
  lists a column's values as checkboxes, but with the data server-side the grid only holds the
  current Window and cannot build that list. This obligation follows directly from choosing a
  model the grid understands; the contract for it is in
  [ADR-0009](./0009-filter-panel-contract.md).
- **The operator set becomes public API.** Adding operators later is easy; changing what one
  means is a breaking change. The semantics are pinned by the reference implementation
  (ADR-0001).
- **The grid needs to know each column's type** — numeric, text and date filters have different
  UI. The type is declared on the column definition rather than inferred.
