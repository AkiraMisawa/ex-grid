# The demo pages call a demo API server, which both hosts share

*(Decided with the user, 2026-09-30, in the ExPivot grilling — Q39, Q46 to Q49, Q62 and Q63. It
refines [ADR-0019](./0019-one-repository-many-packages.md)'s demo hosts.

My first recommendation was to put the database pages on the Server host only. The user rejected
it: "an application in WebAssembly that fetches its data from a server is surely a real case — isn't
that what ExGrid was built for?". It is: it is what `GridSource.Fetch` exists for. The
recommendation was withdrawn.)*

The decisions in ADR-0063 to ADR-0067 all involve a server: a database read into a Snapshot, a Pivot
Source answered by SQL, data that changes on the server, and a server that says what changed. The
demo has no server. The WebAssembly DemoHost is a development server for static files, and the
Server host renders pages but offers no API. The user asked for an example of each case in the
demo, "so that application developers have it easy".

## A process of its own

**`samples/ExGrid.DemoApi` is an ASP.NET Core application: a minimal API, SQLite and SignalR.**

- **It runs as a process of its own.**
- **The pages on both hosts call it over HTTP, with the same code.**
- **It allows the hosts' origins (CORS).**
- **It is a Consumer's server, not part of any package.** Nothing references it, and it references
  the packages as any application would.

Three shapes were weighed (Q48, as revised):

- **A separate API server — chosen.** Page code is identical on both hosts, and layer 3 runs the
  same pages against both. Separating the static site from the API is also an ordinary production
  shape.
- **Turning the WebAssembly DemoHost into an ASP.NET Core application that serves both the client
  and the API** — rejected. It would change the fixture that ADR-0019 kept standalone on purpose,
  and the Server host would still need another way to reach the database.
- **Adding WebAssembly-rendered pages and an API to the Server host** (a Blazor Web App's mixed
  render modes) — rejected. It is another way of starting the host, and it adds assumptions to
  layer 3 that no Consumer shape needs.

## The database is the server's

A browser never connects to a database (Q46). A connection from the browser would put credentials
in the browser, and it would expose the database's port.

**SQLite is therefore a file the demo server creates and owns.**

- **At first start, the server generates 1,000,000 trades from a fixed seed**, showing its progress
  as it goes. The count is 1,000,000 because the user called a million-row CSV normal (Q2, Q49).
- **`EXGRID_DEMO_TRADES` sets another count**, such as five million to see the limits, or fewer for
  a quick test run.
- **The file is never committed.**
- **Money is stored as integer cents**, so the database's own `SUM` is exact.

SQLite in WebAssembly was considered and rejected. It needs native tooling to build. It needs
JavaScript, which is a new ADR-0021 entry, to keep its file. And held only in memory, it is no
more than a Snapshot.

## What it serves

| Endpoint | For | Shows |
|---|---|---|
| The trades as an Arrow stream | `/pivot-db`, "database → Snapshot" | a `DbDataReader` read into a Snapshot, written as Arrow ([ADR-0064](./0064-a-snapshot-travels-as-apache-arrow.md)) |
| Aggregate, Items, Details | `/pivot-db`, "database → server Pivot Source" | `PivotJson` questions answered by SQL written by hand. The Hidden Items become `WHERE`, the Source Version is a change counter, and the Details are `LIMIT`/`OFFSET` pages. Its features leave out the Aggregations SQLite cannot answer exactly ([ADR-0065](./0065-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)) |
| Live on and off, and a SignalR hub | `/pivot-live`, `/grid-live` | the server changing trades and moving the Source Version on, and the hub saying so: the version for a pivot, and the changed trades for a grid ([ADR-0066](./0066-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md), [ADR-0067](./0067-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)) |

- **Live updates are off until a page turns them on**, so a page that reads the data sees it hold
  still.
- **SignalR is the demo's dependency, not a library's** (Q63). ExGrid and ExPivot never learn how a
  notice arrived, because the Consumer passes on what it received. The documentation shows polling
  as well.
- **The pages find the server at their own port plus 3000**, unless configured otherwise.
  Two checkouts on two ports therefore never share a server, which is the rule layer 3 already
  follows for hosts.

## Six pages, one per use case

(Q62, asked for as "an example of each use case".) **Each page says which case it serves and which
API it uses, in code an application developer can copy.** Each runs on both hosts and under both
Chromes.

| Page | Shows |
|---|---|
| `/pivot` | The basics: typed field declarations, the Layout menu, the Details tabs, and switching to the Japanese words |
| `/pivot-csv` | A CSV read under a declared Schema, and an unknown file read under a suggested Schema once the user confirms it. It also shows choosing a file, progress, cancelling, and a malformed row refused by name |
| `/pivot-db` | The same SQLite data two ways, side by side: "database → Snapshot" over Arrow, and "database → server Pivot Source" over `GROUP BY`. The Details are fetched in pages |
| `/pivot-live` | A Change Batch fed to the bundled source, and a server source whose data the API keeps changing. Changed values are highlighted in both |
| `/pivot-risk` | A rate-delta report: desks and curves in Rows, tenors in Columns, ordered by an Order Key as ON, TN, 1W, 1M … 30Y |
| `/grid-live` | ExGrid alone, with no pivot. A live Window, and the server's "trade T100123 changed" notices answering the Change Highlight |

## Consequences

- **ADR-0019's tree gains `samples/ExGrid.DemoApi`**, a Consumer's server that nothing references.
- **Layer 3 starts the API server with the host**, and so does CI. The page list in
  `navigation.spec` gains the six pages.
- **The first start generates the data.** A test run passes a smaller count, so its first start
  stays short.
- **The packages never reference the demo server**, and the package check does not pack it.
