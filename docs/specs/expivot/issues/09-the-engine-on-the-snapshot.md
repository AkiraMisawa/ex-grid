# 09: The engine on the Snapshot

Status: done

**What to build:** `ExPivot.Engine` rebuilt to aggregate a Snapshot's columns (ADR-0059, ADR-0063).

- **Only the parts the Value Fields ask for are accumulated.**
- **Integer and Decimal sums are exact**: scaled 64-bit integers, with `decimal` where needed and
  `double` only when an exact sum overflows.
- **The leaf index hashes well.** The first engine's `(row << 32) | column` key hashed to
  `row ^ column`, which made a large layout super-linearly slow: 23 s against 3.3 s with a mixing
  hash, at a million records.
- **Items come from the column's kind.** Text is folded ignoring case, and its label is the first
  spelling to arrive among the records present.
- **The Order Key**: a key per Item, with null keys last and a function that throws refused by name.
- **The date parts**: year, quarter and month declared from a Date column.
- **Pivot Fields are declared over Snapshot columns**, and a typed declaration builds both the
  column and the field.

Everything ADR-0059 already pinned stays pinned.

**Blocked by:** exgrid-data 01

- [x] PV-3, PV-4 on the new engine; the existing layer-1 tests kept green or rewritten with their ADR
- [x] PV-31: the Order Key's clauses
- [x] PV-32: the date parts
- [x] The leaf index hashes with mixing, with a test on a 270-by-12 layout that would collide under
  `lo ^ hi`

## Comments

2026-10-01: Built. The engine reads a Snapshot's columns and nothing else; the records' source
builds one (`RecordColumns`), and the record-walking aggregator is gone. Every earlier layer-1 test
is green; two that counted accessor calls now count the rows a question reads.

- **The pass** reads each slice column by column, a chunk at a time: each placed field's Items,
  the rows a Hidden Item or the version leaves out, the leaf, then each field in Values.
- **Items.** Text folds the column's dictionary ignoring case; codes are counted per held row,
  and an Item is spelled by the lowest code some held row carries — the first spelling to
  arrive among the records present. A number is keyed by its value as a `double`, in every kind
  and at every slice's scale; a non-finite Double is `#NUM!`; a date is its ticks, found through a
  per-day cache before the clock is hashed.
- **The leaf** is found through the Items packed into one key: a direct table up to 20 bits, a
  map hashed with MurmurHash3's `fmix64` past that, a trie of (node, Item) keys past 62 bits.
  `LeafIndexTests` hold all three to a grouping of the records; 270 dates by 12 books stay in the
  direct table, and the map spreads 12-by-270 `(node, Item)` keys where `lo ^ hi` gives under 512
  hash codes.
- **Exact sums are integers.** Each leaf sums a run of a slice's 64-bit values across slices of one
  scale; a run is folded into a 128-bit integer at a power of ten when the scale changes, when 64
  bits would not hold it, and at the end. Finished, the sum is a `decimal` without trailing zeros —
  or Excel's `double` when no decimal holds it exactly, never a decimal rounded quietly. Past 128
  bits it is a `double` from then on. Only the parts asked for are accumulated; Text, dates and
  Booleans in Values are counted, and Sum is 0.
- **Typed declarations** (`PivotFields.Of<T>()`): `Text`, `Number` over `decimal?`, `double?`,
  `long?` or `int?` (Decimal, Double or Integer), `Date` over `DateTime?`, `DateOnly?` or
  `DateTimeOffset?`, `Boolean`, `Year`/`Quarter`/`Month`, `Key`; `PivotSource.From(records, fields)`
  and `FromAsync`. The README leads with them.
- **The Order Key** (`PivotField.OrderKey`) is read where the report is laid out and where Item
  lists are ordered: once per Item, ties by label, unkeyed Items after the keyed ones, `(blank)` and
  `#NUM!` never keyed, a throwing key refused ("The Order Key of Tenor failed on '7Y'."), keys of
  two types refused by name. A sort by value never reads it.
- **Date parts** (`PivotField.DatePartOf`, `PivotDatePart`) are numbers, labelled through
  `PivotDateWords` — ids `date-year` (`{0}`), `date-quarter` (`Qtr{0}`), `date-month-1` to
  `date-month-12` (`Jan` to `Dec`) — and ordered by the calendar. A date part is captioned by its
  name, as any field. A record behind a cell carries a part as its number.

Measured (4 vCPUs, Intel Xeon 2.8 GHz, .NET 10.0.12, CoreCLR, Release, warmed; never gated;
`Measurements`, run with `-explicit only`). A million records of the demo's trade shape:

| | median |
|---|---:|
| Building the Snapshot from typed declarations, 13 columns (with a Record Key) | 203 ms (216 ms) |
| Main layout: 3 row fields, Product, two money Sums — 2,976 leaves | 23 ms |
| … with Month, a date part, in Columns — 26,784 leaves | 43 ms |
| 270 dates in Columns — 199,511 leaves | 111 ms |
| Main layout, money held as `decimal` in every slice | 56 ms |
| First question over untyped accessors (builds the Snapshot, boxing) | 363 ms |
