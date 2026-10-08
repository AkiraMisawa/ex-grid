# Server pivots send report Windows and share the local engine

*(Decided with the user, 2026-10-06, continuation Q3 on `claude/live-data-next`, after the
incremental A/B experiment and the WebAssembly memory diagnosis. The user accepted server-side
report computation (B), with the same incremental engine running in the browser for local CSV
data. The boundary is decided; its detailed contracts and implementation remain pending.)*

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

**Amended 2026-10-07:** ADR-0154 deliberately changes the write-conflict policy. The requirement
below to preserve historical seen-text judgement applied to the original memory repair; it
is superseded for the continuation that removes value history. Detached report ownership and
correct target/gesture evidence remain required, and the earlier measurements remain evidence
of the original defect rather than proof about the revised implementation.


The [controlled memory diagnosis](../../verification/2026-10-06-macos-pivot-memory/README.md)
found that ExGrid's retained paints keep `PivotReportRow` objects, each reaching a whole Report
generation. At 401,001 Report Rows, full-GC live memory grew by about 151.5 MiB per generation
and update seven failed. Cutting that reference path alone stopped the observed growth, but
discarding paint history would violate
[ADR-0142](./0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md).

The existing CSV page loaded 1,000,000 records successfully, and a separate low-cardinality
million-record fixture completed 70 updates. Source record count alone did not explain the OOM.
Moving work to a server is not the fix for that ownership defect. The retained-row/history
design must preserve the exact historical seen-text judgement while avoiding obsolete whole
Reports retained merely by their displayed rows. No new cap or refusal policy was accepted
instead of investigating and repairing this defect.

## What this decision leaves to the continuation

*Updated after Q5 to Q7, 2026-10-06: ADR-0152 permits replacing the existing APIs, puts custom
remote Order Keys on the server, and chooses automatic current-Window recovery. Those questions
are no longer open.*

- Immutable report versions, reusable rows and keys, Change Highlight, and the ownership of
  the historical evidence needed by ADR-0142.
- Incremental dependency coverage, structural changes, and when a fresh computation is needed.
- Concrete message and source types implementing ADR-0152's identity, atomicity and recovery.
- Operations reaching outside the Window, including copy and Selection Summary, without
  weakening their current contracts; Details still refers to the report's Source Version.
- The public source/report API and the server's report-state lifetime. ADR-0152 permits breaking
  and renaming the existing API; an old Leaf Aggregate compatibility path is not required.

The earlier Leaf Aggregate contract remains documented as the existing API, with its replacement
authorized by ADR-0152. ADR-0066's rejection of server report Windows and ADR-0067's deferral of a delta
transport no longer govern this new path. Their correctness rules still apply: one complete
batch, stale answers discarded, a Stale Report identified, and no changed value presented as
what the user previously saw.

Section 32 gains LV-20 to LV-22 for the shared engine, the server Window boundary, and the
known retention regression. These are requirements to implement, not completed verification.
