# 18: The demo API server

Status: ready-for-agent

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

- [ ] `tests/ExGrid.DemoApi.Tests`: the SQL source's answers equal `PivotSource.From`'s over the
  same trades (PV-22)
- [ ] Layer 3's configuration starts the server with each host, and CI does the same

## Comments
