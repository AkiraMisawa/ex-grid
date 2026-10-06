# 03: ExPivot keeps its report across live redraws

Status: in-progress — ADR-0151 to ADR-0153 accepted; implementation started

**The aim:** a live redraw costs what changed, not what the report holds. Today ExPivot builds its
cube, its report and every report row again on every redraw, at most four times a second. At 401,001
report rows building the report alone took 337 ms (D10, ADR-0140). ag-grid keeps its row nodes,
their ids and their rendered rows across updates; this ticket takes that approach for the pivot,
where the ADRs allow it.

**Measurement prerequisite complete:** 01 established which steps dominate at each size; the
memory diagnosis and A/B boundary comparison are also complete. Ticket 02 is decided in ADR-0150.
The continuation's design is accepted in ADR-0150 to ADR-0153; implementation is authorized.

**Decided, continuation Q3:**
[ADR-0151](../../../adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md)
puts report computation on the server for server data, sending the requested Window and its
changes, and uses the same incremental engine in the browser for local CSV. The history retention
defect must be fixed without losing the old-display evidence ADR-0142 requires. The exact
immutable-row design and dependency coverage remain open. Q5 to Q7 subsequently settled API
freedom, server-registered Order Keys and automatic recovery in
[ADR-0152](../../../adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md).

## What ExPivot does on every redraw

Each step is in `src/ExPivot/Components/ExPivot.Asking.cs`.

- **The bundled source folds the Change Batch into its Leaf Aggregates** and answers
  (`SnapshotPivotSource.Apply`, ADR-0067). This step is already incremental. ag-grid's pivot mode
  re-buckets its filtered input, but can retain the changed aggregation path when its result
  columns remain the same (research note, §2.4); that is not a measured performance comparison.
- **The cube is made again from the whole answer** (`PivotEngine.CubeAsync`, `:392`). Its axis trees
  are built leaf by leaf (`PivotCube.TreeAsync`, `src/ExPivot.Engine/PivotCube.cs:208`), and each
  node's Item hash and path hash are made with it (D10).
- **The report is laid out again** (`PivotEngine.ReportAsync`, `:544`; `ReportBuilder`). Every row is
  a new `PivotReportRow`, with a new `PivotRowKey`.
- **The new rows are compared with the old ones** (`HasSameRowsAsAsync`, `:554`), and the label
  widths are measured again (`LabelWidthsAsync`, `:557`).
- **`Show` (`:636`)** extends the Change Highlight's history, and hands the grid a new delegate for
  each data version: `_cellChangedAt`, `ExPivot.Report.cs:464`.
- **The grid checks every row's key** (`RequireDistinctKeys`; ticket 02), and renders.
  - Rows are now kept by key (ADR-0140, PV-42), so a changed row repaints in place.
  - Every painted row still renders. The rows are new instances, and the delegate is new; a row
    compares the delegate by reference (`src/ExGrid/Components/ExGridRow.razor:320`).

## What ag-grid keeps, read in its source

All links are at ag-grid 36.2.0, commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`. The research
note has the rest: §2.2 for the changed path, §2.4 for pivot mode.

- **A row's id is computed once**, when its data is set, and kept on the node
  ([`rowNode.ts#L468-L493`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/entities/rowNode.ts#L468-L493)).
- **A group's id is its parent's id, its column and its key**, made when the group is made
  ([`groupStrategy.ts#L523`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L523)).
  An update finds the group already there and keeps it
  ([`#L492-L494`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-enterprise/src/rowGrouping/groupStrategy/groupStrategy.ts#L492-L494)).
- **The map of ids is checked only when a node is made**
  ([`clientSideNodeManager.ts#L304-L320`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L304-L320)).
- **Aggregation follows the changed path** when it remains valid. Pivot mode re-buckets its input;
  changed result columns invalidate the aggregation path and require aggregating every group (§2.4).

**The mechanism relevant here is that a row node, its id and its rendered row outlive an update.**
The family's existing incremental leaf fold and ag-grid's pivot pipeline were not benchmarked
head-to-head, so this note does not rank their aggregation performance.

## What the design must respect

- **ADR-0003.** A changed row is a new instance, so that it repaints. An unchanged one may keep its
  instance, and then skips its render.
- **`PivotReportRow` is a new row per report, by contract.**
  - The contract is written at `src/ExPivot.Engine/PivotReport.cs:239`.
  - A row points at its report (`Report`, set by `Attach`, `:276`/`:280`).
  - A row caches its values on first read (`_cells`, `:244`), from its report's cube.
  - ExPivot ties a context command to the report on screen
    (`ReferenceEquals(context.Row.Report, report)`, `src/ExPivot/Components/ExPivot.Report.cs:564`).
  - A row shared by two reports would answer with the older report's values and point at the wrong
    report, unless the design changes all of this.
- **ADR-0060.** The engine is the reference, and a report is immutable. A next report may share what
  did not change (CLAUDE.md's principle 2: an immutable base plus a thin difference); it is never
  rewritten in place.
- **ADR-0066 and ADR-0067.** Leaf Aggregates; never half a batch; the Stale Report. A layout change,
  new caps, or a batch the source refuses to fold (a compaction, `MaxLeaves`) start afresh.
- **ADR-0068.** The Change Highlight's delegate is the change signal, and today a data version makes a
  new one. Unless one delegate is held across data versions, a kept row still renders (M3).
- **ADR-0140.** The Row Key, `PivotRowKey`, and PV-42 and PV-43.
- **PV-21's targets, and PV-40.** The work after an answer is sliced, and yields every 30 ms.

## Decisions to put to the user

*Updated after continuation Q3, 2026-10-06. The boundary and shared incremental engine are
decided in ADR-0151; ticket 02's vouch is decided in ADR-0150. Reusing rendered rows only after
a complete rebuild would not meet the requested incremental computation.*

1. **What a retained row owns.** Separate the engine's immutable report versions from small
   immutable display rows, or change the public engine-row contract. Both the Report back-reference
   and `PivotRowKey`'s axis-node reference reach large graphs today. Historical seen-text evidence
   must remain correct, and unchanged display rows must skip rendering. A stable Change Highlight
   delegate needs an explicit per-row change signal; changing its hidden answers alone is invalid.
2. **Which changes need broader work.** Stable-Item values, Items appearing/leaving, value sort,
   layout changes and compaction; dependencies such as % of Grand Total may legitimately affect
   many report cells. Fresh-computation equality is required, not a choice to relax.
3. **The remote report contract.** Report/query/version identity, whole-batch deltas, recovery when
   a baseline is missing and obsolete responses are decided by ADR-0152. The concrete source and
   message types implement that contract; the Consumer manages server report-state lifetime.
4. **The public API and operations outside the Window.** Compatibility with the existing source
   and synchronous Report APIs is not required (ADR-0152); choose types for the new boundary.
   Copy, Selection Summary and Details keep their established semantics. Remote Order Keys run
   on the server, with explicit Culture and display settings.

The correctness oracle is already required by ADR-0151/LV-20: random batch sequences produce
the same values, labels and order as fresh computation. A raw value change hidden by formatting
still updates the semantic value; a Source Version change with unchanged display and order must
not force every display row to be replaced. The detailed row and protocol contracts must cover
these cases before implementation starts.

## Done when (to be confirmed by the ADR)

- [x] The decisions above, recorded in the branch's reserved ADR block (0150 to 0159; see the
  spec), with criteria added to §32 and the affected §29 criteria updated
- [x] Ticket 01's ExPivot measurement repeated: the redraw at each size, before and after
- [x] M3 repeated: the rows mounted and rendered per redraw on `/pivot-live`'s generator
- [x] The property test
- [x] PV-21's observation in the browser (`measure-pivot.spec.mjs`), recorded beside the old one

## Comments

2026-10-06: Opened by the user's request, after D10 showed that the Row Key's remaining cost and
most of a large redraw's time come from building every row again, which ag-grid avoids by keeping its
nodes.

2026-10-06: Ticket 01's [measurements after PR #64](../../../../verification/2026-10-06-macos-live-update-costs/README.md)
put the 401,001-row Cube at 74.221 ms and Report at 156.859 ms on CoreCLR (minimum of nine
isolated observations). Those constructions dominate; keeping only the rendered row components
does not remove them. On published WebAssembly, each of three independent runs completed six
updates and failed with an out-of-memory exception on the seventh. The cause of that failure
has not been established; retaining structures is not yet evidence of a fix.

The pinned ag-grid source also distinguishes re-bucketing from aggregation: pivot re-buckets
the filtered input, but can retain the changed path for aggregation when its result columns
have not changed. The bundled source's existing incremental fold is not a measured head-to-head
performance comparison with ag-grid.

2026-10-06, continuation Q2: the user asked for more than keeping rendered rows: a change should
recompute only the report portions that depend on it, and send only the changes to the receiving
side. Large data is expected to be supplied by a server. The all-data-in-the-browser case must
also avoid the observed out-of-memory failure under repeated updates. The transport boundary
(changed Leaf Aggregates or completed report changes), supported impact cases, baseline/recovery
contract and memory limits remain questions for the grilling; this comment records the requested
outcome, not an architectural decision or an unlimited-memory guarantee.

2026-10-06, continuation Q3/Q4: the user considers server-computed report Windows/deltas (B)
promising but asks for a measured comparison with Leaf Aggregate deltas and report computation on
the receiving side (A) before deciding. Neither design is accepted yet. Compare like-for-like
incremental work, and distinguish initial load, live changes, interaction round trips, wire bytes
and retained memory; comparing today's full rebuild only with an incremental server prototype
would not answer that question.

The user did not accept a new cap/refusal policy as the answer to the observed failure. They want
the cause investigated until it is known: a browser should handle a CSV of roughly a million source
records, and the investigation must distinguish an architectural limit from object/class design
or implementation defects. Diagnose the existing reproducible WebAssembly OOM, separating source
record count from leaf/report cardinality, before proposing a remedy. No new memory-limit or
server-computation decision is implied by this request.

2026-10-06, memory diagnosis: the [controlled WebAssembly experiments](../../../../verification/2026-10-06-macos-pivot-memory/README.md)
identified the retention path: ExGrid's 64-paint history holds painted `PivotReportRow` instances;
each row owns its entire Report, which owns all Report Rows and its Cube. A full replacement
Report on each redraw therefore keeps a large generation alive for each retained paint.
At 401,001 Report Rows, managed memory after full GC grows by about 151.5 MiB per generation.
Forced GC alone, disabling Change Highlight alone, and reducing the batch from 1,000 changes
to one each still fail on update seven. Clearing only the paint history allows 20 requested
updates with normal GC; the forced-GC variant plateaus near 606 MiB with three live Report
generations. Clearing that history violates ADR-0142 and is a diagnostic intervention, not a fix.

The existing CSV page successfully loaded a 12-column, 1,000,000-record, 85 MB generated CSV
with 111 Report Rows and no cap changes or console errors. A separate 1,000,000-record live
fixture with 1,000 leaves / 1,101 Report Rows completed 70 updates unchanged. These observations
separate source cardinality from report cardinality: they do not establish a universal CSV
size guarantee. The reproduced OOM is an ownership/lifetime defect compounded by whole-report
replacement, not evidence that browser-side aggregation inherently cannot handle a million
records. A remedy must retain ADR-0142's exact historical seen-text judgement; making old
Reports mutable, discarding history or simply forcing GC does not satisfy it.

2026-10-06, computation boundary experiment: the [restricted incremental A/B benchmark](../../../../verification/2026-10-06-macos-pivot-boundary-bench/README.md)
runs the same exact decimal Sum model on either side of HTTP, with the same 40-row renderer.
For 400,000 leaves and 150 ms added RTT, median request-to-frame opportunity is 2,960.3 ms
versus 217.3 ms initially (A/B), 176.9 versus 174.7 ms for a 1,000-leaf batch with one visible
leaf, 27.5 versus 177.8 ms for expansion, and 295.5 versus 211.2 ms for value sort. Initial
transfer and client retention favor B; a small local interaction avoids its round trip in A.
This custom row-wise JSON protocol is not shipped PivotJson, and the model excludes general
aggregations, structural changes and full ExPivot rendering. It supports a boundary decision;
it does not establish finished-product performance or memory safety. B remains undecided.

2026-10-06, continuation Q3 accepted: after the comparison and diagnosis, the user confirmed
server report Windows/deltas and the shared local engine for CSV. ADR-0151 records that boundary,
updates ADR-0066/0067's earlier server-only Leaf Aggregate assumption, and adds LV-20 to LV-22.
The preceding comments are the evidence and state before that answer; this acceptance supersedes
their statement that B is undecided. No new memory cap was accepted. Remaining decisions concern
the row/history model, incremental impact and fresh-computation cases, version/recovery contract,
operations spanning unloaded rows, source API compatibility and server report-state lifetime.

2026-10-06, next external-contract frontier (proposals, not decisions):

- Preserve the existing Leaf Aggregate `PivotSource.Fetch` as an explicit compatibility path,
  or require migration to the new report source contract.
- For remote reports, require custom `OrderKey` policies to be registered on the server, with
  explicit Culture and display settings, or require client-only definitions to remain sufficient.
  A C# delegate is not a serializable query. Culture does not select the language of `Label`.
- When a delta baseline is missing, automatically obtain a complete current Window and use
  Stale Report/Retry if that fails, or wait for explicit Retry from the start. An operation
  naming an older report must never silently receive the newer report's answer.

Full-range copy and Selection Summary, Source Version-correct Details, unchanged-order Selection,
and report immutability are existing requirements, not optional reductions to fit the Window.
Their remote implementation must include asynchronous, versioned access beyond the painted range.
The known retention defect can be addressed with detached display rows/keys without promising
that arbitrary Consumer Action payloads and arbitrary Row Key objects retain no object graph.

2026-10-06, continuation Q5 to Q7 accepted: the user permits changing the API and retiring its
names where better names fit; an old Leaf Aggregate compatibility path is not required. Custom
remote Order Keys are registered server-side, and display settings remain explicit. Missing
delta baselines automatically recover a complete current Window; failed recovery shows the
last complete report as stale with Retry. Older-version operations never receive a newer answer
silently. ADR-0152 and LV-23 to LV-25 record these decisions, superseding the proposals above.

## Completion of the design after Q5 to Q7

*Accepted with the user in Q8 and Q9, 2026-10-06; recorded by ADR-0153. Implementation is pending.*

- The component consumes a report source with the same asynchronous operations locally and
  remotely. Its public state is the current report metadata and Window; direct synchronous
  access to all rows is not simulated for the remote path. Engine access remains a separate
  concern. Keep the existing package dependency direction: pure report operations do not make
  `ExPivot.Engine` depend on ExGrid or Blazor.
- Each report computation owns its current aggregate/index state. Published Report Versions are
  immutable and share unchanged state. The data provider's lifetime is separate from each
  report's calculation state; independent reports must not replace each other's held query.
  The Consumer owns server state storage and disposal. Losing that state uses ADR-0152 recovery,
  not an obligation to retain all past reports or maintain a permanent connection.
- Display rows and their keys have no reference back to a whole Report, Cube or branching axis
  tree. An unchanged display row is reused. A Report Version belongs to the Window's envelope,
  not to every row, so a version-only change does not replace all row instances. Historical
  seen-text evidence remains immutable; Change Highlight has a stable lookup with immutable
  per-row change information. This does not promise that arbitrary Consumer Action payloads or
  arbitrary Row Key objects retain no graph.
- Ordinary additions, changes and removals, including Items arriving/leaving and value sorts,
  update affected leaves, row-ancestor/column-ancestor aggregates and report structure. Avoid
  assembling a fresh full `PivotAnswer` merely to describe those changes. All existing
  Aggregations and the three existing percentage modes remain supported.
- A changed denominator can invalidate an entire Value Field's percentages; sort uses those
  shown values too. This is a legitimate broad dependency, not a reason to rebuild unrelated
  trees or rows. Floating-point sums, Product and Variance preserve fresh-computation results:
  affected aggregates may need re-merging from their contributing leaves in the original
  order. That may include every leaf for a grand total, without rereading every Source Record
  or rebuilding every Report Row. No reassociation of arithmetic is authorized.
- New aggregation questions and source replacement initialize a new result. Compaction keeps
  ADR-0067's existing reset boundary because it changes the source's physical membership
  indices. Explicit display/layout changes rebuild what their dependencies require; a collapse
  does not rerun input aggregation. Running totals, rank and difference are not existing
  features and are not added by this continuation.
- A provider that supplies only a notification of a new Source Version cannot identify changed
  leaves. The user accepts an explicitly declared full-refresh provider; incremental guarantees
  apply to providers that supply change information. A full-refresh path may not silently claim them.

Copy and Selection Summary over unloaded rows use versioned asynchronous operations, while
Details keeps its captured version as already required. Those operations must not materialize
all report values just to display a Window, and a viewport-sized answer must never be substituted
for a whole selection's answer. Type names and storage structures can be chosen during
implementation under Q5; they are not additional user naming decisions.

2026-10-06, implementation: `PivotComputationSession` retains each report's aggregate pass,
immutable cell pages, axes and weighted report structure. `LocalPivotReportSource` supplies
metadata, Windows and versioned offscreen operations; `FetchingPivotReportSource` uses the same
contract over the Consumer's transport. ExPivot's local convenience parameter is `DataSource`;
its `Source` accepts a report provider. `PivotReport.ValueAt(row, column)` reads an immutable
engine version; detached `PivotDisplayRow` values do not retain that version. The demo SQL
provider declares full refresh explicitly.

The [follow-up measurements](../../../../verification/2026-10-06-macos-live-report-after/README.md)
repeat the three report sizes and M3. The original 401,001-row browser reproduction now completes
200 updates with the unmodified 64-paint history and normal GC. A separate million-record live
fixture completes 70 updates, and the actual 85 MB million-record CSV loads successfully.
These are observations of the stated fixtures, not an unlimited memory guarantee. Random
structural batches, all existing Aggregations/percentage modes and extreme decimal cases are
checked against fresh computation, including immutable older answers.
