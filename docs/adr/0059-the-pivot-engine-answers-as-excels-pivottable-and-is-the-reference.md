# The pivot engine answers as Excel's PivotTable does, and is the reference implementation

*(Proposed 2026-09-30 with [ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and not yet decided with the user. Several rules below are **readings** of Excel, not
observations. Each one is listed in `docs/specs/expivot/excel-behaviours.md` with the procedure
that observes it on Windows, as ExSheet's were. An observation that disagrees rewrites the rule
here, and the engine follows.)*

`ExPivot.Engine` computes a Pivot Report from Source Records under a Pivot Layout. It has no UI and
no dependency, so a server computes the same report the screen shows. **Like `GridSource.From` for
filter and sort ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)),
its behaviour is the specification**: a pivot query answered by a Consumer's server, when there is
one (ADR-0058, reserved), is held to it.

The rule of [ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)
carries over: **it answers as Excel does, or it says it cannot**. A report is read for money, and a
plausible total that is not Excel's is the failure to rule out.

## Items

An **Item** is one distinct value of a Pivot Field standing in Rows, Columns or Filters, taken over
the whole snapshot, not only over the records the other fields' filters leave.

- **Text is compared ignoring case.** Excel puts `East` and `EAST` in one Item; the label is the
  spelling of the first record that carries it. Spaces are significant. *(Reading.)*
- **A number is its value.** An `int` 1, a `decimal` 1.0 and a `double` 1 are one Item. A
  non-finite `double` is not a number Excel could hold: it is the Item `#NUM!`.
- **A date is its clock value.** `DateTime` by its ticks (its `Kind` ignored), `DateOnly` as
  midnight of that day, `DateTimeOffset` by its local clock value. Two offsets of one instant are
  two Items; the Consumer who means instants passes UTC.
- **A Boolean is `TRUE` or `FALSE`**, as Excel labels it.
- **No value is a Blank** — the accessor returned null — and its Item is labelled `(blank)`. **An
  empty string is a value**, an Item with an empty label, as ADR-0023 has it for filters.
- **Any other type is Text**, by its culture-formatted text: an enum is its name.

The kind of an Item comes from the value, not from the field's declared type. The declared type
decides only defaults: where a ticked field goes, which Aggregation a new Value Field takes, and how
an Item is labelled.

**Labels.** Text as it is; a number by the field's format under the report's culture, or its
shortest round-trip text; a date by the field's format, or the culture's short date, with the time
when there is one; a Boolean as `TRUE` / `FALSE`; a Blank as `(blank)`.

**Hidden Items are written as keys**: the Item's kind and its invariant text (`1234.5`,
`2026-09-30T00:00:00`, `TRUE`, the text itself). A saved Pivot Layout therefore reads back the same
Items under any culture.

## Order

- **Ascending by default.** Numbers, then dates, then text, then Booleans, then `#NUM!`, then
  `(blank)`. Within a kind: numbers and dates by value, `FALSE` before `TRUE`, text by the report
  culture's comparison ignoring case, ties broken ordinally so the order is total. *(Excel has no
  separate date kind — a date is a number there — so the cross-kind order is ExPivot's.)*
- **Descending** reverses it, and `(blank)` stays last, as it does in both directions for a Grid
  Source (ADR-0023).
- **A field may declare an Item order** — Excel's custom lists, the months of a year. The declared
  values come first, in the declared order; the rest follow in the order above.
- **By a Value Field**, ascending or descending: an Item's place is its value at its total across
  the other axis, as shown (after Show Values As). Blank and error values go last, and ties fall
  back to the label, ascending.

## Hidden Items

- **A record whose Item is hidden in any placed field — Rows, Columns or Filters — is left out of
  the report**, of every cell, every subtotal and every grand total. That is Excel's behaviour for a
  PivotTable over a range (the "include filtered items in totals" option exists only for OLAP).
- **A field holds what is hidden, not what is shown**, so an Item that first appears in a later
  snapshot is shown, as Excel's default is.
- **Hiding every Item of a field is refused** by name. Excel disables OK for it; a report of nothing
  would read as "no data".

## Aggregation

A value is aggregated over the records where a row position and a column position cross. **A
total — a subtotal or a grand total — is computed from all its records, never from the totals below
it**, so the grand total of an Average is the average of the records, as in Excel.

A **number** is a value of a .NET numeric type. Text that looks like a number is text — Excel does
not sum numbers stored as text — and a Boolean or a date is not a number here. *(Excel sums date
serials. Max of a date field is later: ADR-0058's table.)*

| Aggregation | Over the numbers | Records, but no number | Some value, none numeric |
|---|---|---|---|
| **Sum** | their sum | blank | `0` |
| **Count** | the count of values that are not Blank, numbers or not (Excel's `COUNTA`) | blank | the count |
| **Average** | sum ÷ count | blank | `#DIV/0!` |
| **Max**, **Min** | the largest, the smallest | blank | `0` |
| **Product** | their product | blank | `0` |
| **Count Numbers** | the count of numbers (`COUNT`) | blank | `0` |
| **StdDev**, **Var** | sample, of two or more; `#DIV/0!` for one | blank | `#DIV/0!` |
| **StdDevp**, **Varp** | population | blank | `#DIV/0!` |

*("Records, but no number" means every value was Blank. The two right-hand columns are readings.)*

- **No record at all is an empty cell**, for every Aggregation, Count included, as Excel leaves it.
- **Money stays exact.** Integral and `decimal` values are summed, averaged and compared in
  `decimal`. The whole Aggregation falls back to `double`, Excel's own arithmetic, only when the
  field also holds a `double` or `float` in that group, or when the `decimal` sum overflows. Product
  and the four variance Aggregations are computed in `double`.
- **Error values** are `#DIV/0!` (as above, and a Show Values As divisor that is zero or empty) and
  `#NUM!` (a non-finite number in the data, or an overflow in `double`). An error value is painted as
  its text, centred as Excel centres it, and copied as its text. It never becomes a number.
- **The default Aggregation** of a new Value Field is Sum for a field declared Number, and Count for
  every other. *(Excel picks Count when a column holds any text or blank; ExPivot has the declared
  type to go on instead.)*

## Show Values As

Applied to a Value Field after aggregation. In the first version:

- **% of Grand Total** — the value divided by the Value Field's grand total.
- **% of Column Total** — the value divided by its column's total, the value at the grand total row.
- **% of Row Total** — the value divided by its row's total, the value at the grand total column.

A divisor is the aggregate over the records that total covers, whether or not the total is shown. A
blank value stays blank, an error value stays the error, and a divisor that is zero, blank or an
error gives `#DIV/0!`. The number format defaults to `0.00%`.

## The report's shape

- **The Compact form** (the default, as Excel's): one label column, headed `Row Labels`; each level
  indented by one em; a **group row** for each outer Item, carrying its subtotal when subtotals are
  at the top; a `±` button on every outer Item.
- **The Outline form**: one label column per row field, headed by the field's caption; a group row
  for each outer Item, its label in its own field's column.
- **The Tabular form**: one label column per row field; no group rows — an outer Item's label stands
  on the row of its first child; subtotals are always at the bottom.
- **Repeat Item Labels** fills the outer columns of every row in the Outline and Tabular forms.
- **Subtotals** are per field: Automatic (the Value Field's own Aggregation over the group's
  records) or none. The innermost field has none — its rows are its own. **At the top** (the
  default) a subtotal is carried on the group row; **at the bottom** it is its own row,
  `<item> Total`. The Tabular form puts them at the bottom whatever the setting.
- **Grand totals**: a `Grand Total` row at the bottom and a `Grand Total` column at the right, each
  on unless switched off. *(Excel calls the row "grand totals for columns" and the column "grand
  totals for rows". The layout names what is painted: `GrandTotalRow`, `GrandTotalColumn`.)*
- **Collapse is held per Item of a field**, as Excel's `ShowDetail` is, with a field-wide default
  that Expand / Collapse Entire Field sets. Collapsing `East` collapses it wherever it appears. A
  collapsed Item's row carries its totals, in every form and whatever the subtotal setting, and a
  collapsed column Item becomes one column. The innermost field cannot be collapsed.
- **Column items are Header Groups** (ADR-0032). An outer Item's rectangle covers its descendants'
  columns; its subtotal column follows it outside the rectangle, headed `<item> Total`, and its
  header stretches up to the rectangle above it.
- **No row field**: no label column, and one row of totals. **No column field**: one value column
  per Value Field, headed by its caption.

## Σ Values

With two or more Value Fields, their captions form a pseudo-field, **Σ Values**, which stands in
Columns — where Excel puts it — or in Rows.

- **In the first version Σ Values is always the innermost level of its Area.** Excel lets it stand
  anywhere. At an outer position a subtotal splits into one per Value Field above the fields it
  totals, which needs rules of its own; they wait (ADR-0058's table).
- **In Columns**, every column position splits into one column per Value Field, headed by its
  caption. A subtotal's and the grand total's columns stand under a rectangle labelled
  `<item> Total` / `Grand Total`.
- **In Rows**, an Item's row carries no values, and one row per Value Field follows it, labelled by
  its caption, one level deeper. A subtotal and the grand total are one row per Value Field,
  `<item> <caption>` and `Total <caption>`.

## Captions and words

- A Value Field is captioned `<Aggregation> of <field caption>` — `Sum of Amount` — unless it has a
  caption of its own. A caption a second Value Field would repeat takes a number, `Sum of Amount2`,
  as in Excel. **A caption of its own that equals another Value Field's, or a Pivot Field's, is
  refused**, as Excel refuses it ("PivotTable field name already exists").
- The words a report paints — `Row Labels`, `Grand Total`, `Total`, `(blank)`, `(All)`,
  `(Multiple Items)`, `Values`, the Aggregations' names — are English by default and each is
  replaceable by the Consumer, by id.

## Aggregation is kept; layout is redone

**What is aggregated depends only on the records, the placed fields with their Hidden Items, and
the Value Fields' fields.** Collapse, order, the form, subtotals, grand totals, Show Values As,
formats and captions only lay the result out. The engine therefore keeps what it aggregated (a
**cube**) and lays it out again when one of those changes; expanding an Item, sorting it or
changing a Value Field from Sum to Average is not a pass over the records. Each field used in
Values is accumulated once — count, numbers, sum, extremes, product and the running variance — and
every Aggregation is read from that.

**A value cell is computed when it is first read.** ExGrid reads only the rows it paints, so a
report of many rows costs its rows, not its rows × its columns.

## Consequences

- **`ExPivot.Engine`'s tests pin every rule here** (layer 1), and name this ADR. The readings are
  marked in their test names until they are observed.
- **The reference implementation is the engine, not the component.** Show Details, the Item lists
  of the item filter and every Field List rule are the engine's functions, so a server and the
  screen cannot disagree about them.
- **What Excel does and ExPivot does not** — Σ Values at an outer position, collapsing one column
  Item, date and number grouping, Max of dates — is listed in ADR-0058's table, never approximated.
