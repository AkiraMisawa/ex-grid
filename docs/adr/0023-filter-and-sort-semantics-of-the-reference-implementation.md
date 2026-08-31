# Filter and sort semantics of the reference implementation

[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) made `GridSource.From` the
reference implementation: it actually performs filtering and sorting, so it **decides the
meaning** of the operators, and server-side implementations are written to match. Until now that
meaning was undecided. This ADR pins it. The tiebreaker throughout is the product's own claim —
`Ex` names Excel-like operability — except where Excel itself is inconsistent or where
determinism wins; every deviation is recorded below as a deviation.

## The operator set

`FilterOperator` is public API ([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md):
adding operators later is easy, changing what one means is a breaking change):

```
Equals, NotEquals,
GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual,
Contains, DoesNotContain, StartsWith, EndsWith,
In,
IsBlank, IsNotBlank
```

The core decides which operators a column offers
([ADR-0009](./0009-filter-panel-contract.md)), from the column's declared type:

| Column type | Allowed |
|---|---|
| Text | Equals, NotEquals, Contains, DoesNotContain, StartsWith, EndsWith, In, IsBlank, IsNotBlank |
| Number, Date | Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, In, IsBlank, IsNotBlank |
| Boolean | Equals, In, IsBlank, IsNotBlank |

`In` is how the value-list mode of the filter panel serialises, and the value-list mode is
declared per column with no type excluded ([ADR-0009](./0009-filter-panel-contract.md)) — so
every column type offers `In`; a Boolean column's value list is true / false / (Blanks). There is deliberately no
`Between` — it is two clauses combined with And inside one column's `FilterSpec`, and a second
spelling for the same query would make serialised Queries ambiguous. There are deliberately no
dynamic date operators (Today, ThisMonth, …) — a Query must mean the same thing every time it is
deserialised ([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md));
they can be added later against a recorded need, the same stance as the JS allowlist
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

## The pinned semantics

**String comparison is `OrdinalIgnoreCase`, everywhere — filter operators and sort alike.**
Case-insensitivity is the Excel-visible behaviour (`contains apple` finds "Apple"). Between
ordinal and linguistic comparison, determinism decides: ordinal gives the same answer on every
machine, culture and ICU version, and is the only collation a server-side implementation can
reproduce exactly. A serialisable Query deserves one meaning.

**A Blank matches only `IsBlank` — and an `In` whose list explicitly contains it.** Every other
operator, **including `NotEquals` and `DoesNotContain`, does not match a Blank**. One learnable
rule, and it is SQL's rule (three-valued logic: `NULL <> 'Done'` is unknown, and unknown rows
are dropped), so server implementations match without translation. A **Blank** is the accessor
returning null; an empty string is a value, not a Blank — a Consumer that wants `""` treated as
Blank normalises it to null in the accessor.

**Blanks sort last, in both directions.** Exactly Excel's behaviour, and the useful one: the
data being sorted for is on top either way.

**Sort is stable.** Rows with equal keys keep their incoming order — for `From`, the order of
the Consumer's array. Excel's sort is stable and LINQ's `OrderBy` is stable, so this costs
nothing; the test pins it so it stays a promise rather than an accident.

**Multi-column sort is the `Sorts` list in priority order.** First entry primary, then
`ThenBy` chaining; direction and blanks-last apply per level; ties after the last level fall
back to stability.

**Numbers are compared as `decimal`.** The accessor may return int, long, double, decimal, … —
all are normalised to decimal, so a mixed column still orders correctly and equality does not
inherit floating-point surprises. A value decimal cannot represent (NaN, infinity, or a
magnitude beyond decimal's range) is refused with an exception naming the column rather than
quietly ordered somewhere — this grid displays money and risk numbers.

*(Refined while implementing: "beyond decimal's range" includes the **small** direction. A
non-zero float or double whose magnitude converts to zero decimal (below about 5e-29) is
refused, not quietly flattened — a flattened value would match `Equals 0` and tie with true
zeros in a sort, a plausible-looking wrong answer.)*

**Booleans order false before true ascending** (Excel: FALSE < TRUE).

**Date equality is exact value equality of what the accessor returned.** No implicit truncation
to day granularity: two Queries that look equal must not mean different things. Day-granularity
filtering belongs to the accessor (declare the column Date and return the date part).

*(Refined while implementing: `DateTime` compares by its wall-clock ticks — `DateTimeKind` is
not part of the value. This matches .NET's own `DateTime` comparison and what a SQL server does
with the same data, so server-side implementations agree for free. A Consumer whose data mixes
Utc- and Local-kinded values must normalise in the accessor; the grid cannot guess which
instant a Kind was meant to name.)*

*(Refined again while fixing review findings — the other two permitted date types were
running on their native comparisons without those semantics being written down, so they are
pinned here rather than left to drift: `DateTimeOffset` compares and equals by the instant it
names — the offset is presentation, so two stored values with different offsets naming the
same moment are Equal and tie in sort — and `DateOnly` compares by its day. Both are the
types' own .NET semantics, the same stance as `DateTime` above; a Consumer wanting
offset-preserving distinctions normalises in the accessor. Additionally, the one-date-type
rule is now held against every cell a filtered Date column reads, eagerly at extraction —
whether mixed-type cell data is refused must not depend on which operator happens to
compare.)*

**A mismatch between declared type and accessor value is refused.** An unknown column name in a
Filter or Sorts, or an accessor returning text where the column is declared Number, throws an
exception naming the column. Rather than be quietly wrong — comparing values under the wrong
rules produces plausible-looking, incorrect results — say it cannot be done.

**The Opaque Filter is ignored by `From`.** Its meaning exists only Consumer-side
([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md)); an in-memory
reference implementation cannot evaluate it and does not pretend to.

## Rejected

- **Culture-aware comparison (`InvariantCultureIgnoreCase` or CurrentCulture)** — linguistically
  nicer (é sorts near e), but the result then depends on the ICU version and, for
  CurrentCulture, on the machine's locale. The reference implementation would stop being a
  reference. Revisit only with a recorded need, as a per-column opt-in, never as the default.
- **Excel's blank handling for negated operators** — Excel's custom filter lets `does not equal`
  match blank cells while `greater than` does not: the rule would differ per operator, and every
  server implementation would need `OR IS NULL` bolted onto negations only. The single rule
  above deviates from Excel's negated operators, knowingly.
- **`Between` as an operator** — expressible as And of two clauses; one spelling per query.

## Consequences

- The semantics above are public API. Changing any row of this ADR is a breaking change to
  every server-side implementation written to match — rewrite this ADR when it happens.
- `GridSource.From` keeps the signature ADR-0001 sketches: `From(rows)`. The columns it needs
  for evaluation (value accessor + declared type) are pushed in at bind time by the component,
  through the same kind of channel as sort changes — the markup column declaration stays the
  single source of truth. Before columns arrive the result is the input order unchanged; a
  sort or filter change arriving before columns is refused (it cannot happen through the grid).
- `From` owns bumping the **Row Sequence Version**
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)): after
  re-applying the Query it compares the new visible sequence with the old and bumps only when
  the sequence actually differs — a no-op change does not clear selection. Distinguishing
  replaced-instance-same-identity rows needs Row Identity, which `From` does not yet accept;
  that lands with its update surface and extends this ADR.
