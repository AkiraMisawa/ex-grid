# The Snapshot: `ExGrid.Data` and `ExGrid.Data.Arrow`

Status: ready-for-agent

Decided by [ADR-0064](../../adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)
and [ADR-0065](../../adr/0065-a-snapshot-travels-as-apache-arrow.md), in the ExPivot grilling of
2026-09-30. Exit criteria: `docs/definition-of-done.md` §30, DA-1 to DA-17. This spec synthesises
those decisions; where it and they disagree, they win.

## Problem Statement

A pivot over a million records was too slow to use in the browser: 6.8 s per aggregation, with the
page frozen throughout. Almost all of that time went on walking a million objects — a delegate
call, a box and a string hash for every value. The data arrives in several shapes: the Consumer's
objects, a CSV of a million rows (which the user called normal), a database query, or an Arrow
stream from a data pipeline. Some of that data changes every few seconds. Every bundled source in
the family that walks every row pays the same cost, and would pay it again for each shape.

## Solution

**A Snapshot**: an immutable copy of tabular data at one version, held column by column.

- Text is dictionary-encoded, and money is held as exact scaled integers.
- There are four ways in, each of which either yields a Snapshot or refuses by name: objects, a
  CSV under a declared Schema, a `DbDataReader`, and columns.
- A Change Batch keyed by a Record Key makes the next version, sharing everything it did not touch.
- The Snapshot lives in `ExGrid.Data`, which has no dependency.
- It travels as Apache Arrow through the optional `ExGrid.Data.Arrow`.

## User Stories

1. As a developer, I want to turn my list of records into a Snapshot with typed accessors, so that
   nothing is boxed and the records stay mine, by reference.
2. As a developer, I want to read a known CSV under a Schema I declare, so that `00123` stays text
   and a date is read in its own format.
3. As a developer, I want a Schema suggested for a file nobody has described, and never applied
   unless I hand it back, so that the user confirms what the columns are.
4. As a developer in Japan, I want to read the Shift-JIS CSV that Excel saves, by declaring it, so
   that nothing is garbled.
5. As a developer, I want a malformed row to fail the load, naming its row and column, so that a
   total is never quietly short.
6. As a developer, I want progress and cancellation on every load, so that a million rows load in
   the browser without freezing it.
7. As a developer, I want to read a database query through its `DbDataReader`, so that any ADO.NET
   driver, and Entity Framework Core, feeds a Snapshot with no guessing.
8. As a developer with live data, I want to apply a batch of added, changed and removed records by
   key, so that the next version costs the size of the batch, not of the data.
9. As a developer, I want a batch that names a missing key refused whole, so that no version is
   half applied.
10. As a developer, I want to send a Snapshot from my server to the browser as Arrow, and to read
    Arrow that Python, DuckDB or Polars produced, so that my data pipeline needs no new format.
11. As a developer, I want a compressed Arrow stream refused unless I supply the codec, so that I
    download codecs only when I need them.

## Implementation Decisions

All of these are recorded in ADR-0064 and ADR-0065; they are summarised here.

- **The kinds are Text, Decimal, Double, Integer, Date and Boolean**, and a Blank is possible in each
  of them.
  - Text is a dictionary in order of first appearance, with exact values.
  - Decimal is held as scaled 64-bit integers when every value fits, and as `decimal` otherwise.
  - Date is a clock value in ticks.
- **Columns are stored in segments, so that a Change Batch shares every segment it did not touch.**
  A changed record keeps its place in the order, and an added one goes at the end. A dictionary only
  grows.
- **Builders work in slices and yield between them.** Each takes a `CancellationToken` and reports
  progress. A load that meets an unreadable value fails whole, naming the row and the column.
- **The CSV reader works on bytes.** It follows RFC 4180, matches the header per declared column,
  and applies the formats and blank strings the Schema declares. An empty field is a Blank.
  - UTF-8 is read with or without its byte-order mark.
  - Shift-JIS is read through an opt-in encoding that alone refers to the code pages.
- **The `DbDataReader` builder reads each column by its own type.** A column of another type is
  refused unless the Consumer declares how to read it.
- **Arrow is read from the IPC stream and file formats, and written as an uncompressed stream.**
  - Types map as ADR-0065's table says.
  - Other producers' dictionaries are remapped to the Snapshot's rules.
  - Captions, the Record Key and the version travel in the schema's metadata.
  - A compressed stream is read only with a codec the Consumer hands in.

## Testing Decisions

- **Layer 1 only**, in `tests/ExGrid.Data.Tests` and `tests/ExGrid.Data.Arrow.Tests`, each test
  named with its ADR.
- **The Arrow tests write streams the way other producers write them**, using `Apache.Arrow`'s own
  builders, as the measurement's self-tests did.
- **The package check reads and writes a stream through the packed packages** (DA-16).

## Out of Scope

- Parquet.
- Adoption by ExGrid's `GridSource.From`, and by ExSheet's Linked Tables. Each needs an ADR of its
  own (ADR-0064).
- Finding the difference between two whole Snapshots by key (ADR-0067, deferred).
- A time zone database for Arrow timestamps in zones other than UTC (refused, ADR-0065).
