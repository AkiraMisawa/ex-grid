# 19: The six demo pages

Status: done

**What to build:** the six pages ADR-0068 lists.

- `/pivot`, updated;
- `/pivot-csv`;
- `/pivot-db`;
- `/pivot-live`;
- `/pivot-risk`;
- `/grid-live`.

Each page states its use case and the API it uses, in code a developer can copy. Each runs on both
hosts and, where it has a pivot, under both Chromes. The pages are added to the page list and to
`navigation.spec`.

**Blocked by:** 13, 14, 15, 16, 17, 18, change-highlight 01

- [x] PV-20, in layer 3
- [x] change-highlight 02 (DC-54, DC-55) on `/grid-live`
- [x] The browser's `HttpClient` turns response streaming off for the Arrow request (ADR-0064)

## Comments

2026-10-01: Built in two halves, in parallel, and merged.

- **`/pivot`** declares its fields the standard way, `PivotFields.Of<DemoPivotTrade>()`, with Month
  a date part of the trade date.
- **`/pivot-csv`** reads the trade export under a declared Schema, and an unknown file under a
  suggested Schema the user confirms, with progress, Cancel and a malformed row refused by name.
  Its samples are written in memory, so it is usable without a file.
- **`/pivot-risk`** orders tenors by the README's `Tenors.Months`, `1Y6M` beside `18M`.
- **`/pivot-db`** pivots the demo database two ways side by side: a Snapshot read over Arrow, and
  `PivotSource.Fetch` over the server's SQL. Both use the server's fields, so a layout gives the
  same numbers in both.
- **`/pivot-live`** folds Change Batches into the bundled source on a timer the page owns, and
  follows the server's changes through the hub's `VersionChanged`.
- **`/grid-live`** is ExGrid alone: a live Window, the changed trades read back by id, and
  `CellChangedAt` answered from them.

Every page shows its code read from its own source (`DemoCode`), so what a page shows is what it
runs, and layer 3 checks each against the source. Building the pages found that a new `Source`
and a new `Layout` handed to `ExPivot` in one render were refused (fixed in the component), and
that live changes spread over a million trades almost never reached `/grid-live`'s rows (every
other change now falls on the first 500 trades; ADR-0068).

Layer 3, on Linux under xvfb with the container's Chromium (no Chrome or Edge installed; the Edge
project is CI's), each page under both Chromes: `pivot`, `pivot-csv`, `pivot-risk`, `pivot-db`,
`pivot-live`, `grid-live` and `navigation` pass on both hosts, with a clean console.
