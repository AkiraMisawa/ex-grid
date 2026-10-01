# A Snapshot travels as Apache Arrow, through `ExGrid.Data.Arrow`

*(Decided with the user, 2026-09-30, in the ExPivot grilling — Q51 and Q51a, on the measurement
below. The user's first answer was "our own format is fine, but I want to push harder on Arrow".
The measurement showed that choosing Arrow gives nothing up.)*

A Snapshot built on a server sometimes has to reach a browser application, which then aggregates
it — for example, a Snapshot read from a database
([ADR-0063](./0063-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)).

- **It travels as an Apache Arrow IPC stream.**
- **The stream is written and read by an optional package, `ExGrid.Data.Arrow`.** The package
  references `ExGrid.Data` and the Apache Software Foundation's `Apache.Arrow`.
- **`ExGrid.Data` keeps no format of its own.**

The first reason is the user's: **interoperation** (Q51a). A data pipeline in Python, DuckDB or
Polars already speaks Arrow. A server can hand what such a pipeline produces to the browser as it
is, and take a Snapshot back out the same way.

## What was measured

- **The data:** 1,000,000 of the demo's trades, with six text columns, two money columns, a date
  and an integer.
- **The machine:** 4 vCPUs, .NET 10.0.12.
- **The browser:** a published Blazor WebAssembly application without AOT, in Chromium 141.
- **The transfer:** the payload was fetched over loopback, so no network time is included.

| Format | Raw | With HTTP gzip | In the browser: fetch and read into columns |
|---|---:|---:|---:|
| Our own binary format (prototype) | 48 MB | 16.0 MB | 0.63 s |
| Arrow IPC, uncompressed | 64 MB | 16.4 MB | 0.60 s |
| Arrow IPC, with ZSTD buffers | 16.4 MB | 16.4 MB | 1.6 s |
| JSON, one object per record | 190 MB | 26 MB | 37.5 s |

- **Reading Arrow into columns takes 60 ms on CoreCLR**, which covers a Blazor Server host or a
  server.
- **The Arrow reader adds 95.5 KiB to a browser application's download**, Brotli-compressed,
  against 2.8 MB for the whole application. Its LZ4 and ZSTD codecs would add 203 KiB more.
- **Trimming reported no warning from Arrow or its dependencies.**
- **Every path read back exactly the columns built from the records.** That includes dictionaries
  written the way other producers write them — out of order, `amer` beside `AMER`, a null entry —
  which were read under the Snapshot's rules.

**Arrow costs what our own format costs.** The time is the same, and so is the size once the HTTP
compression every server applies has run. Our own format therefore had only one advantage: it has
no dependency. Against that, it needs a specification, a version history, a reader, a writer and
their tests, all kept by us for ever and readable by nothing outside the family.

## The decision

- **The wire format is Arrow's IPC stream**, which `pyarrow`, DuckDB and Polars read and write.
  Arrow's file format (`.arrow`, Feather 2) is read too.
- **The stream is written uncompressed.** HTTP's own compression (gzip, Brotli) does the rest, and
  the browser undoes it natively. Arrow's buffer compression gave the same 16 MB, and took the
  browser 1 s to undo in .NET.
- **A compressed stream from another producer is read only when the Consumer hands the reader a
  codec**, which is `Apache.Arrow.Compression`'s. Only an application that needs the codecs
  downloads them. Without a codec, the stream is refused, and the refusal names the codec it needs.
- **The Snapshot keeps its own layout, and a read converts into it.**
  - Arrow dictionary-encodes a text column, as a Snapshot does. Another producer's dictionary,
    though, may be in any order, repeat a value in two cases, or hold a null.
  - Arrow's decimals take 16 bytes each, where a Snapshot's take 8.

  The conversion is one pass per column, and it is included in the 0.60 s above.
- **Types map one way, and any other type is refused by name:**

  | Arrow | Snapshot |
  |---|---|
  | `utf8`, `large_utf8`, `utf8_view`, or a dictionary of any of them | Text |
  | `decimal32`, `decimal64`, `decimal128`, or `decimal256` within `decimal`'s range | Decimal; a value beyond that range is refused, naming its row and column |
  | `float64`, `float32` | Double |
  | `int8` to `int64`, `uint8` to `uint32`, and `uint64` within `long`'s range | Integer |
  | `date32`, `date64`, or `timestamp` without a time zone | Date, as the clock value written |
  | `timestamp` in UTC, under any of its IANA names | Date, as the UTC clock value |
  | `time32`, `time64` | Date, on the first day |
  | `bool` | Boolean |
  | a null slot, in any of these | a Blank |

  - **A `timestamp` in any other zone is refused.** Converting it would need a zone database, and
    an answer to "whose midnight?". The producer writes UTC or local clock values instead.
  - **Lists, structs, maps and binary are refused**, as is a `uint64` above `long`'s range.
- **Writing gives each column its Arrow type:**
  - Text as a dictionary of `utf8`;
  - Decimal as `decimal128` at the column's scale;
  - Double as `float64`;
  - Integer as `int64`;
  - Date as `date32` when every value is a midnight, and otherwise as a `timestamp` without a time
    zone, in the coarsest unit that holds every value exactly;
  - Boolean as `bool`.

  A Blank is written as a null slot. A column's caption, the Record Key and the Snapshot's version
  travel in the schema's metadata, so a Snapshot round-trips whole.

## What else was learned, for whoever writes a reader

- **Arrow's per-value accessors are what is slow, not the format.**
  - `StringArray.GetString` decodes a new string on every call.
  - `Decimal128Array.Builder.Append(decimal)` took 8 s for a million values in the browser.

  Reading and writing the buffers directly is 10–40 times faster, and that is how the package does
  it.
- **A browser's `HttpClient` streams a response by default.** In .NET 10 that cost about 33 ms per
  megabyte, which came to 1.6–2.1 s for these payloads. With streaming turned off for the request
  (`SetBrowserResponseStreamingEnabled(false)`) it took 0.3 s. The `HttpClient` is the Consumer's,
  so the demo and the documentation show the setting.
- **`Apache.Arrow`'s major version moved four times a year in 2024 and 2025**, reaching 23.0.0 in
  May 2026. The format is stable, but the library's API is not promised to be.
  - The package states the version range it was built and tested with.
  - The package check reads and writes a stream through the packed package, so a break shows there
    first.

## Refined while building it

*(2026-10-01, when the package was built.)*

- **A stream must end with Arrow's end-of-stream marker.** Arrow's specification also lets a
  producer end a stream by closing it, but a stream cut between two record batches would then
  read as whole, with fewer rows: the quietly short total principle 1 refuses. Every producer
  tested — `pyarrow`, Polars, DuckDB and `Apache.Arrow` itself — writes the marker. A file must
  end with its footer for the same reason.
- **The type table grew where interoperation needs it** (Q51a). The first build refused three
  things other tools write by default or in common use, and refusing them would have defeated the
  reason Arrow was chosen:
  - `utf8_view`, and a dictionary of it, is Text. It is what Polars writes by default.
  - The IANA names of UTC — `GMT`, `UCT`, `Universal`, `Zulu`, each also under `Etc/`, and
    `-00:00` — are UTC. Any other zone is still refused.
  - `decimal32` and `decimal64` are Decimal.
  - `time32` and `time64` are a Date on the first day, as a database's `TimeOnly` is
    (ADR-0063).
- **A value that cannot be held exactly is refused, never rounded.** This covers a decimal with
  more than the 28 places `decimal` holds, and a nanosecond timestamp that is not a whole number
  of 100 ns ticks.
- **A read never disposes a record batch.** Arrow shares a dictionary across batches; disposing a
  batch released it while later batches still pointed at it, which refused `pyarrow`'s delta
  dictionaries and crashed on a compressed stream. Buffers are read into managed memory instead.
- **gzip is named when it is found.** A payload that is still gzip-compressed — an `HttpClient`
  without automatic decompression — is refused, saying so, rather than as an unreadable stream.
- **The read of a demo trade carries more cost than the measurement above.** A million trades with
  a unique `TradeId` read in about 0.4 s on CoreCLR, not 60 ms. About 220 ms of that is a million
  distinct texts decoded and kept, and 85 ms is the Record Key's index, which the shape measured
  above did not have. The shape without a unique key still reads in about 70 ms.

## Considered options

- **Our own binary format in `ExGrid.Data`**, with or without Arrow beside it (Q51, options a and
  b) — rejected on the numbers above: it costs the same, and it would be a format we carry alone.
- **JSON records** — rejected. They were 60 times slower in the browser, which grew to 1.6 GB of
  memory reading them. JSON stays where messages are small: a Pivot Source's questions and answers
  ([ADR-0065](./0065-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)).
- **Arrow's buffers as the Snapshot's own memory, read without a copy** (Q51, option d) — rejected.
  Skipping the copy saves 0.1–0.3 s per million rows. In exchange, the Snapshot's rules — one
  dictionary entry per value, in the order it first appears — would be handed to whatever each
  producer wrote, and every money column would take twice the memory.

## Consequences

- **`ExGrid.Data.Arrow` references `ExGrid.Data` exactly and `Apache.Arrow` within a stated
  range.** Nothing in the family references it except the demo and its tests. It ships beside
  `ExGrid.Data` (ADR-0063).
- **Its tests** round-trip every kind with a Blank in each, read streams written the way other
  producers write them, and pin every refusal. §30 of the Definition of Done states them.
- **The demo's API server serves its database as an Arrow stream**, and the `/pivot-db` page reads
  it on both hosts
  ([ADR-0068](./0068-the-demo-pages-call-a-demo-api-server-both-hosts-share.md)).
