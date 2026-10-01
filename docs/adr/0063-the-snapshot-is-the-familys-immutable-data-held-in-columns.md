# The Snapshot is the Ex family's immutable data, held in columns, in `ExGrid.Data`

*(Decided with the user, 2026-09-30, in the grilling of ExPivot's first version — Q22, Q29 to Q35,
Q37 to Q39, Q41 to Q44, Q53 and Q55. It began as a question about making a 1,000,000-row CSV fast
in a pivot. The user saw that the answer was not a pivot's: holding data as columns "looks right
for speed in general — the grid, the sheet and the pivot".)*

A **Snapshot** is an immutable copy of the Consumer's tabular data at one version, held column by
column. It lives in a package of its own, **`ExGrid.Data`**, which has no dependency, so that any
of the family's bundled sources can read one. ExPivot's bundled Pivot Source is the first
([ADR-0065](./0065-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)).

```
the Consumer's data ──► Snapshot   (ExGrid.Data: from objects, a CSV, a DbDataReader, or columns;
                          │         from Arrow through ExGrid.Data.Arrow, ADR-0064)
                          │  a Change Batch makes the next one; nothing is rewritten
                          ▼
                     the bundled sources read it   (ExPivot's first; ExGrid's and ExSheet's by ADRs of their own)
```

## Why columns

These figures were measured during the grilling on one machine: 4 vCPUs, .NET 10.0.12, and in the
browser a published Blazor WebAssembly application without AOT, in Chromium 141. The data was
1,000,000 records, with three row fields, one column field and two money Value Fields.

| | CoreCLR | Browser |
|---|---:|---:|
| The first engine, over the records (`Func<TRecord, object?>` per value) | 687 ms | 6.8 s |
| The same layout, over columns (prototype) | 33 ms | 0.20 s |
| … with Month added to Columns | 44 ms | 0.27 s |
| … summing money as scaled 64-bit integers | 10–12 ms | 37–59 ms |
| Building the columns from 1,000,000 objects, with typed accessors | 0.34 s | 2.3 s |
| Reading a 74 MB CSV straight into columns | 0.47 s | 3.2 s |
| Memory: the records / the columns | 114 / 65 MiB | 80 / 65 MiB |

The cost was the pass over a million objects, not the arithmetic: each value took a delegate call,
a box and a string hash. Held as columns, a text value is a small integer and a number is a slot in
an array, so a re-aggregation is a loop over arrays. The same holds wherever a bundled source walks
every row: `GridSource.From`'s sort, filter and value lists
([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)), and a Linked
Table read by `SUM(Positions[PV])`
([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)).

## What a Snapshot holds

- **Columns, each of one declared kind**: **Text**, **Decimal**, **Double**, **Integer**,
  **Date** or **Boolean**. Each column has a name, unique within the Snapshot, and a caption.
- **A Blank, in any kind**: a value that is not there. It is kept apart from every value, the
  empty string and zero included.
- **Text is held as a dictionary.** Every distinct value appears once, in the order it first
  appears, and each row holds a code into the dictionary. Values are kept exactly as written, so
  two spellings are two entries. A reader that tells text apart ignoring case, as a pivot's Items
  do ([ADR-0059](./0059-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)),
  folds the dictionary, not the rows.
- **Decimal is held exactly.** When every value fits in 64 bits after scaling by one power of ten,
  it is held as those scaled integers. A sum is then an integer addition, which was measured 3–5
  times faster with the same result. Otherwise it is held as `decimal`. A Decimal column holds
  values, not the scale each was written with: `1.5` and `1.50` read back alike.
  *(Refined 2026-10-01, when it was built: the power of ten is chosen for each stored segment of
  the column, not once for the whole column. A Change Batch may bring a value with more places,
  or one that does not fit, and a scale for the whole column would mean rewriting every segment
  the batch did not touch. Each segment says its own scale, and a reader sums each segment at
  its scale.)*
- **Double is held as `double`**, non-finite values included, exactly as they came. What they mean
  is the reader's to say; a pivot shows `#NUM!`.
- **Integer is held as a 64-bit integer.**
- **Date is held as a clock value.** A `DateTime` is its ticks, with its `Kind` ignored. A
  `DateOnly` is its midnight. A `DateTimeOffset` is the clock it shows, with its offset dropped.
  This is the rule ADR-0059 gave a pivot's Items, and it is now the data's own.
- **Boolean is held as true or false.**
- **Its records, when it was built from the Consumer's objects.** The objects themselves are kept,
  by reference and in order, so a reader can hand back the object behind a row, as Show Details
  does.
- **A version**, which each Change Batch moves on.

A Snapshot is data, not a query engine. It does not sort, filter, group or aggregate. The sources
that read it do, each to its own semantics.

## Four ways in

Each way in is a builder that either yields a Snapshot or refuses.

- **A load that meets a value it cannot read fails whole, naming the row and the column** (Q32).
  It never yields a Snapshot with the row left out, because totals over that Snapshot would be
  quietly short.
- **Each builder takes a `CancellationToken` and reports its progress.**
- **Each builder works in slices, yielding between them.** A browser has one thread, and it must
  keep painting while a million rows load.

The four ways in:

1. **The Consumer's objects, through typed accessors** — `Text("Region", t => t.Region)`,
   `Decimal("Pnl", t => t.Pnl)`, `Date(...)`. A typed accessor reads a value without boxing it;
   boxing cost 2.2–2.6 times the allocations (Q53). An untyped accessor, returning `object?` under
   a declared kind, remains for columns known only at run time. The objects are kept, as described
   above.
2. **A CSV, under a Schema the Consumer declares** (Q22, Q31). Nothing is guessed: guessing is what
   reads the account number `00123` as the number 123, a quietly wrong answer.
   - **Per column**, the Schema declares the header the column matches, its caption, its kind and
     how to read it: a date's format, the decimal point and the thousands separator, or a culture
     that gives them. It also declares the strings that count as a Blank, such as `NULL` or `-`.
   - **For the file**, it declares the encoding, the separator (comma, tab or semicolon), and
     whether a header row comes first. Quoting follows RFC 4180.
   - **An empty field is a Blank, in every kind** (Q55), as Excel reads it.
   - **A declared column missing from the header is refused by name.** A column the Schema does not
     declare is skipped.
   - **UTF-8 is read with or without its byte-order mark.**
   - **Shift-JIS is read when the Schema declares it** (Q34). It is what Excel on Japanese Windows
     saves, and `ExGrid.Data` offers the encoding. The code pages it needs are loaded only by an
     application that asks for them, so a browser application that reads only UTF-8 does not
     download them.
   - **The file is read as bytes, straight into the columns**, with no string made per cell.
   - **For a file nobody has described, a Schema can be suggested** from its first rows, with each
     column whose kind is unclear marked (Q33). The suggestion is a proposal to show the user, and
     it is never applied by itself.
3. **A `DbDataReader`** (Q39). This is ADO.NET's reader, which every .NET database driver provides
   and which Entity Framework Core runs on. The reader already knows each column's type, so nothing
   is guessed:
   - a `decimal` is Decimal;
   - a `double` or a `float` is Double;
   - an integer is Integer;
   - a date or a time is Date;
   - a `bool` is Boolean;
   - a `string` is Text.

   A column of any other type is refused by name, unless the Consumer declares how to read it.
4. **Columns directly**, for data the Consumer has read itself, such as Parquet or a message
   stream. Apache Arrow comes in through `ExGrid.Data.Arrow`, which is built on this way in
   ([ADR-0064](./0064-a-snapshot-travels-as-apache-arrow.md)).

**The Order Key and a declared Item order belong to the Pivot Field, not to the Snapshot.** Q31
listed them with the CSV Schema. The Snapshot has since moved into the family's package, and
`CONTEXT.md` defines the Order Key as a Pivot Field's. A Pivot Field declared over a Snapshot
column therefore carries both. The caption is the data's own, so it stays with the column, and a
Pivot Field takes it as its default.

## A Change Batch makes the next Snapshot

Live data was put into the first version (Q41, Q44), and not only for ExPivot: for example, a
blotter a million records long that changes every few seconds. **The rules are written here so that
they hold for every reader.**

- **A Record Key is one declared column, Text or Integer**, whose value tells a record apart from
  every other. Two records under one key are refused, naming the key.
- **A Change Batch carries the records added, the records changed (by their key) and the keys
  removed.**
- **A batch is applied whole, into the next Snapshot, or refused whole.** It is refused by name,
  with nothing applied, when it changes or removes a key that is not there, or adds a key that is.
- **The next Snapshot shares every part the batch did not touch with the one before**, and the one
  before stays exactly as it was. Applying 1,000 changes to a million records copies what those
  1,000 records need, not the million. This is principle 2: an immutable base plus a thin
  difference.
- **A changed record keeps its place in the order, and an added record goes at the end.**
- **A dictionary only grows**, so a code means the same text in every version. Text that no record
  carries any more is simply not carried.
- **A Snapshot without a Record Key takes only batches that add.** Otherwise it is replaced whole.
- **A reader can fold a batch into what it already computed, rather than start again.** It is
  handed what the batch removed and what it added. ExPivot does this
  ([ADR-0066](./0066-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).

## Refined while building the ways in

*(2026-10-01, when the CSV reader and the `DbDataReader` builder were built.)*

- **`ExGrid.Data` is marked trimmable.** Without the mark, a published Blazor WebAssembly
  application that read only UTF-8 still shipped the code pages, 686 KB (170 KB with Brotli),
  because an assembly that is not marked trimmable is kept whole. With it, only an application
  that asks for Shift-JIS downloads them. A test reads the package's metadata and finds the code
  pages referred to by the Shift-JIS encoding alone.
- **The CSV reader is strict, by design.**
  - Headers are matched exactly. A refusal names the near spelling it found ("the header has
    'notional'").
  - An Integer column refuses `12.0`.
  - An empty line among records is refused.
  - A file that starts with a UTF-16 byte-order mark is refused by name. The decision lists UTF-8
    and Shift-JIS, and a file in another encoding is better refused than misread.
- **Each date is read in its culture's own calendar**, so a Thai `2569` is the year 2026. A date
  is the clock it shows: a format with `Z` or `GMT` keeps the clock, and never converts to the
  machine's zone. A format with no year, or an offset with no date, is refused when the Schema is
  checked, because it would otherwise fill in today's date.
- **A `TimeOnly` read from a database is a Date on the first day.** The decision says "a date or a
  time is Date". A `TimeSpan` is refused unless declared, because it is a duration as often as a
  time of day.

*(2026-10-01, when the CSV read was made faster — ExGrid.Data's ticket 07,
`verification/2026-10-01-linux-measure-csv`.)*

- **A million-row CSV now reads in 4.0 s in a published WebAssembly build, from 12.9 s, and in 491 ms
  on CoreCLR, from 692.** Both were measured on the same machine.
  - None of this section's rules moved.
  - The refusals' words, rows and columns are the same, and tests hold the shortcuts to the full
    parsers.
  - In the browser the cost was calls, not arithmetic: a call costs 16–24 ns in the interpreter
    against 1–2 on CoreCLR. So the read was rebuilt around fewer calls:
    - separators found sixteen bytes at a time;
    - a batch of records cut first, then read a column at a time;
    - values appended a batch at a time;
    - texts told apart by their bytes;
    - the stream read 4 MiB at a time, where each read in a browser is a call into JavaScript.
- **A text column's lookup by text is built when it is first asked for**, not at the end of the
  load. The first `TryGetCode`, or the first Change Batch of a Snapshot read from a CSV, pays for
  it once: 74 ms on CoreCLR, and 0.23–0.28 s in a browser, for a million distinct Ids. A CSV is
  mostly read and pivoted, never batched, and the load is spared it.
- **A slice yields with `Task.Yield()` in a browser too**, not with a delay of 1 ms. The delay was
  chosen so that the page could paint, and was never measured against the yield. Measured, the
  yield paints a frame a slice at 0.4–0.6 ms, against the delay's two frames at 4.3–4.4 ms, which
  is about 0.4 s of a million-row read. ExPivot's slices yield the same way (ADR-0065).
- **A crash this found is fixed.** A column of numbers, dates or Booleans with a Blank in its first
  256 rows, followed by enough rows to grow its array, failed with an
  `ArgumentOutOfRangeException` rather than loading.

## One package, family-wide, adopted one product at a time

- **`ExGrid.Data` has no dependency outside the framework.** It carries the family's published
  name, as `ExGrid.MudBlazor` does, so it is found beside the grid.
- **ExPivot reads it first.** `ExPivot.Engine` references it and keeps its own property: it runs on
  a server with no UI.
- **ExGrid and ExSheet adopt it by ADRs of their own, each after measuring.**
  - A grid's rows are the Consumer's objects, and Row Identity is their reference
    ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). A Snapshot serves a
    grid as an index beside those objects, never as their replacement.
  - A sheet's cells are sparse, which columns do not suit. Its Linked Tables are not sparse.
- **It ships with ExPivot, outside ExGrid's release** (Definition of Done §2), until ExGrid adopts
  it. Its criteria are §30.

## Considered options

- **A pivot cache inside `ExPivot.Engine`** — Excel's own shape, and the first proposal (Q29).
  Rejected by the user: the technique is not a pivot's, and a second copy made later for the grid
  would drift from the first.
- **Adopting it in all three products now** — rejected (Q37). The grid's Row Identity and the
  sheet's sparse cells each need a decision of their own, and each is worth measuring first.
- **`System.Data.DataTable`** — rejected. It holds rows of boxed values, mutable in place, with no
  dictionary for text. It has every cost measured above, and none of the immutability the rest of
  the design leans on.
- **Apache Arrow's buffers as the Snapshot itself** — rejected in ADR-0064. Arrow's dictionaries
  come in whatever order and case their producer wrote them, and its decimals take 16 bytes each.
- **Guessing kinds in a CSV** — rejected (Q22), because it turns `00123` into 123.

## Consequences

- **Every rule above is a layer-1 test** in `tests/ExGrid.Data.Tests`, named with this ADR. §30 of
  the Definition of Done states the rules as criteria.
- **`ExPivot.Engine` now references `ExGrid.Data`**, which references nothing
  ([ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md), revised).
- **Part of ADR-0059's rules for reading a value now belongs to the Snapshot**: what a number is,
  a date's clock value, and a Blank. They are stated once, here, and ADR-0059 points to them.
- **A Consumer holds a Snapshot as it holds any immutable value**: in a field, replaced by the next
  one, never rewritten.
