# Server pivots send report Windows and share the local engine

*(Decided with the user, 2026-10-06, continuation Q3 on `claude/live-data-next`, after the
incremental A/B experiment and the WebAssembly memory diagnosis. The user accepted server-side
report computation (B), with the same incremental engine running in the browser for local CSV
data. Built on `claude/live-data-next`, and taken on `claude/live-data-best` on 2026-10-08, when the
user compared the two tracks of live data continued: ExPivot follows this ADR and ADR-0152/0153, the
grid follows the Claude Code track's ADR-0142 and ADR-0160.)*

**For server data, the server computes the Pivot Report and sends the requested Window and its
changes. For local data, including CSV, the same incremental engine runs where the data is held.**
In WebAssembly that is the browser. Moving the computation must not create two implementations
of Excel's rules. `ExPivot.Engine` remains the reference, as
[ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md) requires.

- The server path no longer requires the browser to receive all Leaf Aggregates and construct
  the whole Report. Its live path sends the changes needed for the requested Window, together
  with the metadata needed to interpret it. The exact messages and recovery protocol are a
  subsequent decision; [ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)
  now settles baseline recovery and version identity.
- The local path retains local report computation and local interactions. A local CSV does
  not need to be uploaded to a server to use ExPivot.
- The common engine updates the report portions that depend on the accepted changes. It must
  produce the same report as a fresh computation. The supported incremental cases and cases
  requiring a fresh computation are to be settled before implementation; the benchmark's
  decimal Sum restriction is not a reduction of the product's Aggregations.
- ExPivot remains ExGrid's Consumer. ExGrid does not aggregate or open a server connection.
  The Consumer still owns transport and authentication; this decision does not choose HTTP,
  SignalR, polling, or a transport package.
- In Blazor Server, component code already runs on the host and the browser receives render
  diffs. A browser calling a report server from WebAssembly is a different boundary; the
  experiment below must not be treated as a measurement of Blazor Server.

## Why this replaces the earlier server boundary

[ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)
chose Leaf Aggregates and rejected finished report Windows because the latter made collapse,
sort and form changes server round trips, and required server implementations of the report's
rules. Sharing the engine removes the need for a second implementation; the round-trip cost
remains and is accepted for server data in exchange for lower initial transfer and browser
retention. Local CSV keeps its local interactions.

The [restricted A/B experiment](../../verification/2026-10-06-macos-pivot-boundary-bench/README.md)
used the same incremental decimal Sum model and 40-row renderer on both paths. With 400,000
leaves from 1,000,000 Source Records and 150 ms added RTT, seven-sample medians were:

| Operation | A: browser computes report | B: server computes report |
|---|---:|---:|
| Initial data request to frame opportunity | 2,960.3 ms | 217.3 ms |
| 1,000 changed leaves, one in the Window | 176.9 ms | 174.7 ms |
| Expand | 27.5 ms | 177.8 ms |
| Sort by value | 295.5 ms | 211.2 ms |

The prototype's post-GC managed browser footprint was 39.79 MiB for A and 4.69 MiB for B.
This is not peak memory, browser RSS, or the real component's memory. The ordinary-update timing
ranges overlap: **the measurement does not show a universal update-latency win for B**. B's
benefit here is initial transfer and browser retention, and fewer transmitted values when a
large batch mostly affects rows outside the requested Window.

Its custom row-wise JSON is not shipped `PivotJson`; a different encoding changes A's initial
cost. General Aggregations, Show Values As, structural changes, full ExPivot rendering and
long-running retention were not benchmarked. These observations justify the boundary trade-off,
not a finished-product performance claim or a timing gate.

## Memory correctness remains a separate obligation

The [controlled memory diagnosis](../../verification/2026-10-06-macos-pivot-memory/README.md)
found that ExGrid's retained paints kept `PivotReportRow` objects, each reaching a whole Report
generation. At 401,001 Report Rows, full-GC live memory grew by about 151.5 MiB per generation
and update seven failed. Cutting that reference path alone stopped the observed growth. Moving
work to a server is not the fix for that ownership defect, and no new cap or refusal policy was
accepted instead of repairing it.

*(When this was decided, ExGrid still kept those paints to judge a write against what the user had
seen, and the repair had to keep that judgement. On `claude/live-data-best` the grid no longer judges
a write by what it painted
([ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md), as
rewritten on 2026-10-07) and holds no row beyond its Window
([ADR-0160](./0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)); ADR-0153's
detached display rows hold no report either. The earlier measurements are evidence of the original
defect, not of the merged code.)*

The existing CSV page loaded 1,000,000 records successfully, and a separate low-cardinality
million-record fixture completed 70 updates. Source record count alone did not explain the OOM.

## What this decision left to the continuation, and where it went

*(Updated after Q5 to Q9, 2026-10-06, and on 2026-10-08.)*

- API replacement, server-side Order Keys and automatic current-Window recovery:
  [ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md).
- Immutable report versions, reusable rows and keys, the Change Highlight, incremental dependency
  coverage and structural changes:
  [ADR-0153](./0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md).
- Operations reaching outside the Window — Copy and the Selection Summary by Report Version, Details by
  the Source Version the cell was shown under (settled while merging, 2026-10-08: a layout gesture does
  not take Details away) — ADR-0152.

The earlier Leaf Aggregate contract remains documented as the existing API, with its replacement
authorized by ADR-0152. ADR-0066's rejection of server report Windows and ADR-0067's deferral of a delta
transport no longer govern this new path. Their correctness rules still apply: one complete
batch, stale answers discarded, a Stale Report identified, and no changed value presented as
what the user previously saw.

Section 32 judges this ADR with LV-24 and LV-25, and LV-22's ExPivot clause (LV-20 to LV-22 on the
Codex track, renumbered when the tracks were merged).
