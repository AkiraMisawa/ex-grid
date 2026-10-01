# 18: The demo API server

Status: done

**What to build:** `samples/ExGrid.DemoApi` (ADR-0068).

- **The server itself:** an ASP.NET Core minimal API with SQLite (`Microsoft.Data.Sqlite`) and
  SignalR, allowing the hosts' origins (CORS).
- **The data:**
  - 1,000,000 trades generated at first start from a fixed seed, with progress shown;
  - `EXGRID_DEMO_TRADES` sets the count;
  - the file is never committed;
  - money is stored as integer cents.
- **What it serves:**
  - the trades as an Arrow stream;
  - Aggregate, Items and Details answered by SQL written by hand — Hidden Items as `WHERE`, the
    Source Version as a change counter, Details as `LIMIT` and `OFFSET` pages — with features that
    leave out what SQLite cannot answer exactly;
  - live updates on and off;
  - a hub that says the version changed, and which trades changed.
- **Its port:** the page's port plus 3000, unless configured otherwise.

**Blocked by:** 10, exgrid-data 03 and 05

- [x] `tests/ExGrid.DemoApi.Tests`: the SQL source's answers equal `PivotSource.From`'s over the
  same trades (PV-22)
- [x] Layer 3's configuration starts the server with each host, and CI does the same

## Comments

2026-10-01: The skeleton is in: SQLite trades (money in cents), live updates, the hub, CORS, the
port convention, `/api/status` and `/api/trades`, and layer 3 and CI start it. The Arrow
endpoint and the Pivot Source's three endpoints wait for the packages they answer with.

2026-10-01: Finished. `GET /api/trades.arrows` serves the trades read through
`SnapshotDataReaderBuilder` (cents as Decimal, the ISO text as Date, 0/1 as Boolean, `TradeId`
the Record Key, and the Month the Pivot Source computes) as an Arrow stream, with the Source
Version in `ExGrid-Source-Version`, exposed to other origins; the bytes are built once per version.
`POST /api/pivot/aggregate`, `/items` and `/details` answer `PivotJson` by hand-written SQL inside
one read, each carrying that read's version, and `GET /api/pivot/fields` gives the fields and
features `PivotSource.Fetch` takes. `GET`/`POST /api/trades/by-id` and `POST /api/reset` serve
`/grid-live` and layer 3. PV-22 passes: asked through `PivotSource.Fetch` over HTTP, the SQL source
answers as `PivotSource.From` over the same trades, question for question, and refuses in the same
words. What the build settled:

- **Groups are the stored values** (`GROUP BY Region, Desk`), folded into Items in .NET by
  `PivotItemKey` equality, their parts merged exactly. At a million trades, three text fields
  grouped in 1.4 s, and in 2.0 s `COLLATE NOCASE`.
- **Hidden Items and a cell's Items are compared `COLLATE NOCASE`** where the Item is ASCII: no
  other character equals an ASCII one under `OrdinalIgnoreCase`, so `NOCASE` is the engine's
  comparison there. An Item with another letter uses the engine's comparison registered as a
  collation. A Blank that is not hidden is kept by name (`Currency IS NULL OR … NOT IN …`).
- **A text Item stored in two spellings** is labelled by the first group read, not by the data's
  first spelling as `PivotSource.From` labels it; the numbers agree. The generated trades spell each
  Item once, which a test pins. Choosing the data's first would cost 8–35% per aggregate.
- **Features**: Sum, Count, Average, Max, Min and Count Numbers, and Refresh. Product and the
  variances are not offered: SQLite has neither, and the engine's are doubles whose last bits depend
  on the records' order.
- **Compression**: Brotli first and gzip second, both at the fastest level. Brotli's fastest took
  what gzip's did and was 37% smaller.

Measured on 4 vCPUs, .NET 10.0.12, Release, over loopback:

| | 20,000 trades | 1,000,000 trades |
|---|---:|---:|
| Read into a Snapshot (`DbDataReader`) | 241 ms | 5,960 ms |
| Written as Arrow | 139 ms | 423 ms |
| Arrow stream | 1.63 MiB | 81.3 MiB |
| gzip, fastest | 0.65 MiB, 6 ms | 32.6 MiB, 294 ms |
| Brotli, fastest | 0.41 MiB, 7 ms | 20.4 MiB, 319 ms |
| (gzip level 6, for comparison) | 0.38 MiB, 31 ms | 19.0 MiB, 1,677 ms |
| Served from the cache: identity / Brotli | 10 ms / 11 ms | 0.19 s / 0.38 s |
| An aggregate (Region × Desk by Product, a Filters field hiding) | 25–80 ms | 1.16–1.27 s |
| An Items page (Book; TradeId's first 10,000; P&L's first 10,000) | 8–90 ms | 0.22 s; 0.93 s; 2.3 s |
| A Details page of 100 (a cell; the grand total from 900,000) | 5–27 ms | 0.10–0.19 s; 0.05 s |
| `POST /api/reset` | 25–38 ms | 1.3 s |

Half of the million-trade read is Microsoft.Data.Sqlite's own: a plain read of the same rows took
2.4–2.8 s, one .NET string per text value. The Month column costs 0.6 s of it and 0.76 MiB of the
Brotli payload. An aggregate at a million trades is SQLite's sort, well past PV-21's 0.3 s for a new
question; the bundled source over a Snapshot is the fast path.
