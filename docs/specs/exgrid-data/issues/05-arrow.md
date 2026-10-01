# 05: `ExGrid.Data.Arrow`

Status: ready-for-agent

**What to build:** a new project, `src/ExGrid.Data.Arrow`, referencing `ExGrid.Data` exactly and
`Apache.Arrow` within a stated range (built with 23.0.0). Its tests are
`tests/ExGrid.Data.Arrow.Tests`.

- **Reading:** Arrow's IPC stream and file formats, read into a Snapshot.
  - Work from the buffers, never the per-value accessors, which were measured 10–40 times slower.
  - Remap another producer's dictionary to the Snapshot's rules.
  - Map types by ADR-0064's table, and refuse anything outside it by name.
  - Refuse a compressed stream, naming its codec, unless the Consumer passes a codec factory.
- **Writing:** an uncompressed IPC stream.
  - Text as a dictionary of `utf8`, Decimal as `decimal128` at the column's scale, Double as
    `float64`, Integer as `int64`, Boolean as `bool`.
  - Date as `date32` when every value is a midnight, and otherwise as the coarsest `timestamp` unit
    that holds every value exactly.
  - A Blank as a null slot.
  - Captions, the Record Key and the version in the schema's metadata.

**Blocked by:** 01

- [x] DA-13: a round trip is equal column by column and Blank by Blank, with the metadata
- [x] DA-14: other producers' dictionaries; the type table; the refusals; with and without a codec
- [x] DA-15: the Date units, and no compression
- [x] DA-16: the package check reads and writes a stream through the packed packages
- [x] DA-1: the references as stated

## Comments

Built, 2026-10-01.

- **`SnapshotArrow`**: `ReadAsync(Stream, options, codecs, token)` and `ReadAsync(ReadOnlyMemory<byte>,
  …)` read an IPC stream or an Arrow file (told apart by `ARROW1`) through `SnapshotColumnsBuilder`,
  8,192 rows per checkpoint, so progress, yields and cancellation are the builder's own; from memory
  the buffers are read where they lie. `WriteAsync(Snapshot, Stream, token)` writes an
  uncompressed stream in record batches of 65,536 rows, in `Snapshot.Rows` order, from the slices'
  spans; every value is checked before the first byte, so a refused write writes nothing.
  `StreamMediaType` is `application/vnd.apache.arrow.stream`. `SnapshotArrowMetadata` names the
  keys: `exgrid.caption` (a field's, written only when it is not the name), `exgrid.recordKey` and
  `exgrid.version` (the schema's); a read honours them whoever wrote them.
- **Reading** works from Arrow's buffers. Another producer's dictionary is remapped through
  `AppendCodes`, each entry decoded once per dictionary version, whatever its index width; a
  replaced dictionary and pyarrow's delta dictionaries are read by the batches after them. A
  decimal chunk whose values fit 64 bits goes in through `AppendScaled`; others are made exact or
  refused. Refused by name: every type outside ADR-0064's table (`utf8_view` with what to write
  instead), a timestamp in a zone other than `UTC`, `Etc/UTC`, `+00:00` or `Z` (any case); by row
  and column: a decimal beyond `decimal`'s range or with more than 28 significant places, a
  `uint64` above `long`, a date outside 0001–9999, a nanosecond timestamp finer than 100 ns, a
  dictionary index outside its dictionary, text that is not UTF-8; and bytes that are not Arrow,
  gzip, an empty stream, bytes ending inside the first message, a stream ending before its
  end-of-stream marker (anywhere, between batches included), a file without its footer, duplicate
  or empty column names, rows with no column, and bad metadata. A compressed stream asks the codec
  factory only when it is compressed; without one, the refusal names LZ4 frame or ZSTD.
- **A read never disposes a record batch.** Disposing one released the dictionary its columns
  share with later batches whenever Arrow owned that memory — one concatenated from a delta, or
  undone from compressed buffers — so pyarrow's deltas were refused and a compressed stream crashed
  with a `NullReferenceException`. The stream and file readers now read into managed memory.
- **Writing**: Text `dictionary<int32, utf8>` (the whole dictionary, its codes the indices; an
  entry no held row uses is written with U+FFFD for a lone surrogate, one a row holds is refused by
  row); Decimal `decimal128(38, s)` at the largest scale among the slices the version reads, a
  value past 38 digits there refused; Date `date32` or the coarsest of `timestamp[s/ms/us/ns]`, and
  a column that needs nanoseconds refused for a date outside 1677–2262.
- **Tests**: 144, plus one explicit measurement. Streams from Apache.Arrow's own builders, and
  from pyarrow, Polars and DuckDB, kept under `Producers/` with the script that made them.
  Apache.Arrow's writer sends one dictionary and no deltas, so deltas come from pyarrow.
- **Package check**: `ExGrid.Data.Arrow` is packed into the ExPivot feed, its nuspec read back
  (exactly `ExGrid.Data`, and `Apache.Arrow [23.0.0, 24.0.0)`), kept out of the release feed, and
  compiled into the smoke application; `RoundTrip/` runs a Snapshot of every kind through a stream
  and back through the packed packages and fails the check on any difference.
- **Trimmable**, as `ExGrid.Data` is, with no trim warning. In the published smoke application,
  which both reads and writes, `Apache.Arrow` is 70 KiB and `ExGrid.Data.Arrow` 29 KiB with Brotli.
- **Measured** (4 vCPUs, .NET 10.0.12, CoreCLR, never gated): a million of the demo API's trades,
  keyed by a unique `TradeId` — 75.7 MiB raw, 17.7 MiB with gzip; written in 139 ms, read in
  388 ms from memory and 373 ms from a stream. Of the read, about 220 ms is the million distinct
  ids, decoded and interned, and 85 ms the Record Key's index. ADR-0064's own shape (six text
  columns of few values, two money columns, a date, an integer): 61.2 MiB raw, 10.7 MiB with gzip,
  written in 58 ms, read in 70 ms.
