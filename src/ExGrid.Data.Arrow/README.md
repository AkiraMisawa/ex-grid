# ExGrid.Data.Arrow

A **Snapshot** of `ExGrid.Data` as **Apache Arrow**: written as an uncompressed Arrow IPC stream,
and read from Arrow's IPC stream or file format (`.arrow`, Feather 2) — what pyarrow, DuckDB and
Polars read and write. A server sends a Snapshot to a browser this way, and takes in what a data
pipeline produced as it is.

- **Reading converts into the Snapshot's own layout**, column by column from Arrow's buffers, record
  batch by record batch, in slices that yield between them, with progress and cancellation.
  Another producer's dictionary is taken under the Snapshot's rules: in the order its values first
  appear in the rows, one entry per exact text (`amer` and `AMER` are two), a null entry or a null
  index a Blank.
- **Anything a Snapshot cannot hold exactly is refused by name**, and the read yields nothing: a
  type outside the table below names its column; a value names its row and its column; a stream
  cut short is never read as fewer rows.
- **Writing gives each kind its Arrow type**, a Blank a null slot, and carries the captions, the
  Record Key and the version, so a Snapshot round-trips whole.
- **The stream is written uncompressed.** HTTP's own compression (gzip, Brotli) does the rest. A
  compressed stream from another producer is read when you hand the reader a codec.

> **This is a prerelease (`0.x`).** `ExGrid.Data.Arrow` ships with ExPivot, beside `ExGrid.Data`,
> and is not part of ExGrid's release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0`.
- **`ExGrid.Data`, at exactly this package's version.**
- **`Apache.Arrow` 23.x** — the range this package was built and tested with. Arrow's format is
  stable, but its .NET library's API moves with each major version, so a newer major is taken up
  by a new release of this package, not by a wider range.

## Install

```sh
dotnet add package ExGrid.Data.Arrow --prerelease
```

## Writing

```csharp
using ExGrid.Data;
using ExGrid.Data.Arrow;

// An ASP.NET Core endpoint: the response body is the stream.
app.MapGet("/trades.arrows", async (HttpResponse response, CancellationToken token) =>
{
    response.ContentType = SnapshotArrow.StreamMediaType;   // application/vnd.apache.arrow.stream
    await SnapshotArrow.WriteAsync(snapshot, response.Body, token);
});
```

Rows go out in the Snapshot's order (`Snapshot.Rows`), so a Snapshot that has taken Change Batches
writes what it holds, in record batches of 65,536 rows.

## Reading

```csharp
using ExGrid.Data;
using ExGrid.Data.Arrow;

// In a browser, take the response whole: streaming it costs far more than the read.
using var request = new HttpRequestMessage(HttpMethod.Get, "trades.arrows");
request.SetBrowserResponseStreamingEnabled(false);   // Microsoft.AspNetCore.Components.WebAssembly.Http
using var response = await http.SendAsync(request, token);
response.EnsureSuccessStatusCode();
byte[] payload = await response.Content.ReadAsByteArrayAsync(token);

Snapshot snapshot = await SnapshotArrow.ReadAsync(payload, new SnapshotLoadOptions { Progress = progress }, cancellationToken: token);
```

A read from a `Stream` takes it from its current position to its end-of-stream marker and leaves
it open:

```csharp
await using var body = await response.Content.ReadAsStreamAsync(token);
Snapshot snapshot = await SnapshotArrow.ReadAsync(body, cancellationToken: token);
```

On a server, give the `HttpClient` automatic decompression for what the server compressed with
gzip or Brotli (`HttpClientHandler.AutomaticDecompression`); bytes still compressed with gzip are
refused, saying so.

### Compressed streams

A stream whose buffers are compressed — LZ4 frame or ZSTD, as pyarrow can write — is read when you
pass `Apache.Arrow.Compression`'s codecs, and refused without them, naming the codec it needs. Only
an application that reads such streams references that package.

```csharp
using Apache.Arrow.Compression;

Snapshot snapshot = await SnapshotArrow.ReadAsync(payload, codecs: new CompressionCodecFactory(), cancellationToken: token);
```

## Types

| Arrow | Snapshot |
|---|---|
| `utf8`, `large_utf8`, `utf8_view` (Polars' default), or a dictionary of any of them (any integer indices) | Text |
| `decimal32`, `decimal64`, `decimal128`, `decimal256` | Decimal; a value beyond `decimal`'s range or its 28 places is refused |
| `float64`, `float32` | Double, as it came, non-finite values included |
| `int8` to `int64`, `uint8` to `uint32`, `uint64` up to `long.MaxValue` | Integer |
| `date32`, `date64`, `timestamp` without a time zone | Date, as the clock value written |
| `timestamp` in UTC — `UTC`, `GMT`, `UCT`, `Universal`, `Zulu`, `Greenwich`, `GMT0`, `GMT+0` or `GMT-0`, each also under `Etc/`, or `+00:00`, `-00:00` or `Z`, in any case | Date, as the UTC clock value |
| `time32`, `time64` | Date, the clock time on the first day, 0001-01-01, as a database's `TimeOnly` is read |
| `bool` | Boolean |
| a null slot in any of these | a Blank |

Any other type is refused by name: lists, structs, maps, binary, durations, intervals, `float16`,
and a `timestamp` in another time zone (converting one would need a time zone database). A date
outside 0001–9999, a time outside a day, or a nanosecond timestamp or time finer than the 100 ns a
date holds, is refused by row and column.

Writing gives Text `dictionary<int32, utf8>` (the Snapshot's own dictionary, its codes the
indices), Decimal `decimal128(38, scale)` at the largest scale its slices hold, Double `float64`,
Integer `int64`, Date `date32` when every value is a midnight and otherwise a `timestamp` without a
time zone in the coarsest unit that holds every value exactly, and Boolean `bool`.

## Metadata

| Key | Where | Holds |
|---|---|---|
| `exgrid.caption` | a field | the column's caption, when it is not the name |
| `exgrid.recordKey` | the schema | the Record Key's column name |
| `exgrid.version` | the schema | the Snapshot's version, in decimal digits |

Another producer may write them too; a read honours them whoever wrote them
(`SnapshotArrowMetadata` names them).
