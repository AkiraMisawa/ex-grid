# A remote report's Window is read ahead, sixty rows either side

*(Decided with the user on 2026-10-10 and 2026-10-11, from ticket 14 of `docs/specs/live-data` and two
measurements: `verification/2026-10-10-linux-remote-read-ahead/` and
`verification/2026-10-11-linux-remote-read-ahead-blocks/`.)*

[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md) has a server compute
the Pivot Report and send ExPivot the Window the grid needs. ExPivot asked for exactly the range the
grid asked for. That range is the rows one Viewport holds, with nothing either side
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). So over a network, every
scroll that showed a row outside the Window cost a round trip of Placeholders.
[ADR-0025](./0025-what-the-bundled-fetching-source-promises.md) already reads ahead for ExGrid's
bundled fetching source, `readAheadRows = 60`. ExPivot's remote path was the family's one
Window-fetching path without it.

Measured at 150 ms a round trip, held scrolling showed Placeholders for nearly all of the scroll with
no read-ahead (wheel 99%, ArrowDown 100%). With N rows read either side, each doubling of N roughly
halved the share and roughly doubled the costs that scale with the Window. On the wide report of
about 250 value columns:

| N | 30 | 60 | 120 |
|---|---|---|---|
| Held wheel, narrow / wide | 36 / 40% | 19 / 23% | 10 / 13% |
| Held ArrowDown | 18 / 20% | 8 / 10% | 4 / 5% |
| Server projection per live update | 50 ms | 90 ms | 168 ms |
| Kept Windows' heap | 0.38 MiB | 0.70 MiB | 1.34 MiB |
| Dense answer (all 251 columns filled) | 1.3 MiB | 2.3 MiB, parsed in 411 ms | 3.9 MiB, parsed in 675–761 ms |

## The decision

- **ExPivot reads a remote report's Window ahead.** It asks for the range the grid needs widened by
  the source's read-ahead either side, clamped into the report. The first Window stays as it is.
- **The number lives on the source:** `PivotReportSource.Fetch` takes `readAheadRows`, **60** by
  default. A local report source (`PivotReportSource.From`) declares 0, since it has no round trip to
  hide. This is ADR-0025's reason: how far ahead is worth asking is a judgement about the Consumer's
  data and server. What a read-ahead row costs ranged from 0.6 to 17.7 KiB with the report's columns.
  An ExPivot parameter would read ahead for every source unless each Consumer turned it off.
- **A question whose Window covers the range stays in flight.** A newer range that the question in
  flight already covers does not supersede it, so a held scroll is answered rather than restarted.
  - This departs from ADR-0025's rule, under which any different range cancels the fetch in flight.
  - Under that rule, 60 rows still left Placeholders for 85–88% of a held scroll, because every held
    input cancelled the question before it.
  - `GridSource.Fetch` keeps ADR-0025's rule until its own path is measured
    ([ADR-0183](./0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md)).
- **What ADR-0025 promises otherwise holds here too.** An answer that does not reach the range the
  grid needs is refused by name. The check is made against the range before widening. Window
  Changes and their Window Digest
  ([ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)) cover the
  whole widened Window.

## Considered options

- **No read-ahead.** Rejected: over a network, a held scroll is all Placeholders.
- **120 rows by default.** It halves the held shares again. But it doubles every cost that scales with
  the Window, on every update of a live report and in every ExPivot on the page. A Consumer who would
  rather pay that passes 120.
- **Aligned blocks of 100 rows, as ag-grid's Server-Side row model fetches.** Measured beside N either
  side, and rejected.
  - Over one contiguous Window, blocks bought nothing that N either side of the same size did not. A
    block of 100 asks 200 rows at a boundary, and landed between N = 60 and N = 120.
  - A block of 200 asks 400 rows, and did no better than N = 120's 255.
  - ag-grid's advantage needs a cache of every block fetched (`maxBlocksInCache` = -1). A live
    report's single Window cannot have one, since every update would have to reach every block held.
- **An ExPivot parameter for the number.** Rejected as above.
- **Recording it as a note in ADR-0151.** Rejected with the user: it changes a public contract and
  departs from ADR-0025 for a measured reason.

## Consequences

- **Asking earlier, not wider, is what helped most.** That is
  [ADR-0183](./0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md).
- **Built by ticket 14 of `docs/specs/live-data`;** the criterion is LV-34.
- **Found by the measurement, and left for later:**
  - each answer resends the report's metadata;
  - each question carries its 5.7 KiB of settings;
  - on a live update the server projects every row of the Window, about 0.7 ms a row at 251
    columns.
