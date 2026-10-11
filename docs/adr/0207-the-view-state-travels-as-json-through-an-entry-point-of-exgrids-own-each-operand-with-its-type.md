# The View State travels as JSON through an entry point of ExGrid's own, each operand with its type

*(Decided with the user on 2026-10-11, when the tests written for the Definition of Done's FN-13 and
ST-4 found that the View State does not round-trip. It amends
[ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md) and
[ADR-0016](./0016-column-width-and-overflow.md)'s "Auto widths are not persisted".)*

FN-13 asks that sort and filter travel as a serialisable structured model, and that a `GridQuery`
round-trips through `System.Text.Json` equal. ST-4 asks the same of the whole View State — widths,
order, pinned count, sort, filter — with an Auto width kept as the intent, never as the pixels it
resolved to. Their purpose is ordinary: a user narrows a blotter to "amount over 1,000,000, this
month", widens two columns and sorts by date, and the application keeps that view for the next
visit, or sends the query to its own server.

Written as tests, neither holds:

- **A filter operand loses its type.** An operand is typed `object`. Written by `System.Text.Json` it
  becomes a JSON number or string, and read back it is a `JsonElement`, not the `decimal`, date or
  `bool` it was. It compares equal to nothing, and a filter restored that way cannot be applied.
- **`ColumnWidth` has no JSON form.** A converter written for it was internal, so a Consumer's
  source-generated `JsonSerializerContext` could not reach it (SYSLIB1220).
- **`RowRange`** read back as no rows at 0; it gained a `[JsonConstructor]` and now reads back through
  its own checks.

A saved view is a format users' data is kept in. Once it ships, changing it breaks what they saved.

## The decision

- **ExGrid has an entry point of its own for the View State's JSON**, as ExPivot's `PivotReportJson`
  is for a report: public static methods that write a View State, or a `GridQuery` alone, to JSON and
  read it back. Inside, an internal source-generated `JsonSerializerContext` does the work, so a
  Consumer needs no options, no converters and no context of their own, and the form is the same
  under reflection and trimming.
- **The View State is one value**, so that one call saves a view and one restores it: the sort, the
  filters, and per column — named by its key — its width, its order and whether it is pinned. Its
  names are chosen in the change that builds it, reviewed with it, and public by
  [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md)'s
  rule (a Consumer uses it).
- **Each operand carries its type**: a tag naming one of the operand types the filter model admits
  ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)), and its value
  written in the invariant culture. A `decimal` is written as its exact digits in a string, never as a
  JSON number, so that money survives a reader that would parse a number as a `double`. A date keeps
  its kind; a `DateTimeOffset` keeps its offset.
- **A width is `"Auto"` or a number of pixels.** An Auto column is stored as the intent, as ADR-0016
  decided, never as the width it resolved to.
- **Anything else is refused by name**: an unknown tag, a value that does not parse as its tag's type,
  a width that is neither form, a column key the grid does not have. Reading never guesses a type and
  never drops a part it does not understand (principle 1). What a refusal names is stated in the
  entry point's documentation.
- **The form is versioned from its first release.** The JSON carries a version, and a later version
  that changes the form reads the earlier ones or refuses them by name.

## Considered options

- **Public converters, for a Consumer to put into their own options or context.** It lets a Consumer
  mix the View State into a document of their own, but every Consumer must wire it, and one that
  forgets gets `JsonElement`s back — the quiet failure the tests found.
- **Leave operands untyped, and let the Consumer convert them by the column's type.** It would relax
  FN-13 and ST-4 ("round-trips equal"), and every Consumer would write the same conversion, some of
  them wrongly.

## Consequences

- ADR-0002 and ADR-0016 carry dated notes pointing here.
- FN-13 and ST-4 are verified through the entry point; the held tests on `claude/criteria-tests-held`
  land with it, together with the Docs Site's paragraph on keeping a view, which now names the entry
  point.
- The entry point and the View State's type are added to the public surface and its baseline
  (ADR-0201).
- `CONTEXT.md`'s **View State** gains a line naming the entry point.
