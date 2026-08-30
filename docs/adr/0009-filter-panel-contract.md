# The filter panel contract — Excel's behaviour, the value list is declared per column, and fetching is pull

The grid paints the filter UI itself
([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md)). That UI is
**substitutable** (`IGridChrome`), and the `FilterPanelContext` handed to the substitute is a
fixed public contract. The behaviour follows **Excel**.

```csharp
public sealed record FilterPanelContext(
    ColumnInfo Column,
    FilterSpec? Current,                    // the condition applied to this column
    IReadOnlyList<FilterOperator> Allowed,  // operators available for this column's type; the core decides
    FilterUiMode Mode,                      // ValueList / Condition / Both; from the column definition
    Func<Task<DistinctValues>> RequestDistinctValues,
    Action<FilterSpec?> Apply,              // OK
    Action Clear);
```

## What was decided

### 1. Whether a value list is offered is declared per column (with a runtime safety net)

Excel's filter has two modes side by side — a **list of values** (checkboxes) and a **condition**
(operator plus value). Both are expressible in the structured model of ADR-0002 (the first as
`IN`). What differs is **the data requirement**: a value list needs **every distinct value in the
column enumerated**.

That cost is proportional to the column's cardinality, and cardinality differs by orders of
magnitude between columns. A product-type column might have six values and suit checkboxes; a
**price column has roughly as many distinct values as there are rows**, and enumerating it is
meaningless.

**The grid has no way to know the cardinality.** Since
[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) made the interface push, it
cannot go and count either.

- **Declare it in the column definition** (`FilterUiMode`). The Consumer knows the shape of its
  own data.
- **Add a runtime safety net that can answer "too many".** Declarations drift from reality (a
  column believed to hold 120 values grows to 50,000 after a merge). `DistinctValues` can return
  either the values or **TooMany**, and the panel degrades to condition mode **without breaking**.
- **The degraded form is Excel's own: a search box.** Excel likewise does not list everything when
  the list is large; it lets you search. So the fallback is not "no value list available" but
  "search to narrow it down".

Rejected: **fetch the list at runtime and decide from the count** — that means retrieving a
million values from a price column before giving up.

### 2. The value list reflects filters on other columns, but excludes the column's own

Narrow a book column to a desk's twelve books, then open the currency filter, and **only the
currencies actually present on that desk** should be listed. A filter is a tool for narrowing, and
**an option that yields zero rows is a broken tool**.

**Excluding the column's own filter is the crucial part.** With two product types already ticked,
reopening that same column while honouring its own filter would list only those two — **a dead
end from which the third can never be added back**. Excel excludes the column's own filter for the
same reason.

The precise contract is "**the distinct values under all applied filters except this column's
own**". The Consumer owns the filter state (ADR-0001), so the grid does not need to pass it.

Rejected:
- **No cascading (always the whole dataset)** — cheap and cacheable, but the more you narrow, the
  more zero-yield options appear. "For a fixed-domain column it is better to see everything" has
  some merit, but that is a request for reference information, not for a filter.
- **Per-column choice by the Consumer** — behaviour would vary by column within one grid and users
  could not learn the rule.

### 3. Applied on OK, not on each checkbox

As in Excel. This suits the push interface — **there is no Consumer round trip per checkbox**.
Cancel discards. While the panel is open the edits are uncommitted state, and
[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)'s "uncommitted belongs to the
grid, committed to the Consumer" applies unchanged.

**The value list is not re-fetched while the panel is open.** The list reflects only the
*applied* filters; a list that shifts with every checkbox is unusable.

Columns combine with **AND**. OR exists only within a column (an `IN` list, or "A or B" in a
custom condition).

### 4. Fetching distinct values is pull, deliberately

`RequestDistinctValues` returns a `Task`. **This looks like a contradiction with ADR-0001's push
decision, and it is deliberate.**

Push was chosen for **correctness, not for consistency**. Applying ADR-0001's four reasons to this
particular fetch:

| Problem with pull | Does it apply here? |
|---|---|
| Sorting an overridden column breaks quietly | **No.** The list affects neither ordering nor displayed values |
| Cache invalidation machinery is needed | **No.** A stale list only offers a stale choice; the choice itself still applies correctly |
| View State / Saved View becomes double-booked | **No.** The list is not persisted |
| Does not mesh with state-management libraries | Yes, but that is convenience |

The line:

> **Push is for application state** — what is displayed, what the Overlay affects, what enters
> undo, what gets saved.
> **Pull is fine for transient UI data that cannot break quietly when it is wrong.**

Making it push would put data that disappears when a popover closes into the store, so a Fluxor
Consumer would carry it in State and mix it into undo history for no benefit.

## Consequences

- **`FilterPanelContext` is public API.** If it does not carry what a substitute needs, the
  substitute will reach into grid internals. In MudBlazor's `MudDataGrid`, Consumers have to walk
  `RenderedColumns` to map column GUIDs back to property names for template columns — this
  contract exists to avoid that. Columns are identified by something meaningful, not by a GUID.
- **If the Consumer caches the list, the key is (column, other columns' filter state).** Keying on
  the column alone serves stale lists.
- **A server implementation performs a DISTINCT with the other columns' conditions applied.**
  Heavier than a plain `DISTINCT`, but only used on columns the Consumer declared as value-list
  columns — that is, columns it judged to have low cardinality.
- **The core decides `Allowed` (which operators exist).** Chrome only picks among them and never
  decides what an operator means, which is why substituting Chrome cannot change behaviour. The
  meaning is defined by `GridSource.From` (the reference implementation) and by server
  implementations written to match
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).
- **What happens to the Selection after a filter is applied** is settled in
  [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md): it is
  cleared, because rows disappear and positions shift.
