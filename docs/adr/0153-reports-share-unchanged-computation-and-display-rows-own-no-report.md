# Reports share unchanged computation, and display rows own no Report

*(Decided with the user, 2026-10-06, continuation Q8 and Q9: both recommendations accepted.
This completed the Codex track's design with ADR-0151 and ADR-0152, and ExPivot's vouch for its
report — that track's ADR-0150, which is ADR-0141's section of 2026-10-07 on `claude/live-data-best`.
Built there, and taken on `claude/live-data-best` on 2026-10-08, when the user compared the two tracks
of live data continued; the sections of that day below say what the comparison changed.)*

*(2026-10-09, in the grilling of the merged pull request: the user decided what the history holds and
why (below), and confirmed the full-refresh capability's notice, which had been settled while merging.
The same day a first report's cost was measured and fixed, below. What this record called a delta is
**Window Changes** in the glossary.)*

**Separate report computation from the rows handed to ExGrid.** Published Report Versions are
immutable and share unchanged state. A display row contains its detached Row Key, labels and
the values needed for its requested range, with no back-reference to a Report, Cube or branching
axis tree. Unchanged display rows keep their instances. Source/Report Version metadata belongs
to the Window's envelope, not every row, so a version-only change does not recreate every row.

This removes the ownership path established by the
[memory diagnosis](../../verification/2026-10-06-macos-pivot-memory/README.md): a few historical
painted rows retained whole obsolete Report generations. Merely replacing the Report reference
is insufficient if the Row Key still reaches an axis node's parent and its other children.
Keys retain their role, Value Field and Item equality and their cached hash, using detached
identity data. Reading a key remains allocation-free (PV-43).

## Computation follows dependencies

The common local/server engine handles ordinary additions, replacements and removals, including
Items appearing/disappearing, by updating affected leaves, row-ancestor/column-ancestor
aggregates, axis paths and report structure. An ordinary batch does not rebuild a complete
`PivotAnswer`, Cube, axis tree or Report merely to describe its changes. Unchanged structure is
shared. The engine's private working indexes may be mutable; published versions are not.

- All existing Aggregations and the three existing percentage modes remain supported.
  Incremental results equal a fresh computation, including labels and order.
- Item spelling changes can affect labels and ordering even when Item equality is unchanged.
  Structural changes update affected row intervals, Tabular boundary labels and Header Group spans.
- Value sort follows the shown value, including its percentage dependencies. An affected sibling
  order is updated; moving a subtree does not recompute its unrelated cell values.
- A changed Row/Column/Grand Total invalidates its dependent percentages. A Grand Total may
  affect the entire Value Field. Unrequested cells remain lazy; broad dependencies do not imply
  rebuilding unrelated structure.
- Exact parts can be updated by subtraction/addition where the reference permits it. Double
  sums, Product, Variance and other non-invertible parts preserve fresh-computation order. An
  affected aggregate may need its contributing leaves re-merged, possibly every leaf for a
  grand total. That is distinct from rereading all Source Records or rebuilding all Report Rows.
  Reassociating arithmetic and changing rounded results is not authorized.
- New aggregation questions and source replacement initialize a new result. Snapshot compaction
  retains ADR-0067's reset boundary because physical membership positions change. Explicit
  layout/display changes rebuild what they depend on; collapse/expand does not reaggregate
  the input. Running totals, rank and difference are not added by this work.

## The Change Highlight remains correct

*(When this was decided, ExGrid kept what it had painted, to judge a write against what the user had
seen, and this section required that history to stay whole while it stopped holding obsolete Reports.
On `claude/live-data-best` the grid no longer judges a write by what it painted
([ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md), as
rewritten on 2026-10-07) and keeps no paint history at all
([ADR-0160](./0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)). Detached ownership,
immutable versions and the Change Highlight rules below remain.)*

ExPivot uses one stable Change Highlight lookup with immutable per-row change information, so
unchanged rows skip rendering. A new lookup is not created for every Source Version. A changed
raw value hidden by formatting still updates the semantic value without a false highlight.
Highlight expiry retains the grid's existing timer behavior. The required historical state is
bounded to its purpose and must not retain an unbounded chain of obsolete reports.

**What the history holds** *(measured while merging, 2026-10-08; decided with the user, 2026-10-09)*.
The report source keeps the reports the Change Highlight compares:
- every Source Version published less than `ChangeHighlightDuration` before the newest, by the
  source's clock;
- the version before the oldest of those, as their baseline.

These always include the `versionsKept` newest, two by default. At most max(`versionsKept`,
⌈`ChangeHighlightDuration` / the time between redraws⌉ + 1) reports are alive. With /pivot-live's
one-second highlight that is two when redraws come a second apart, and five when they come every
250 ms. A layout gesture clears the history back to the newest.

Versions share what an incremental update did not change, but a redraw built afresh is a whole copy.
A long highlight over fast redraws therefore costs memory in proportion, and
[ADR-0161](./0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md) says what
happens when it runs out. LV-22 checks the bound by reachability. The Claude Code track's first
ADR-0161 kept times in place of reports, and was not taken with this design.

**Why reports, and not times** *(decided with the user, 2026-10-09)*. The Change Highlight is a state of
the cell, asked by row and column ([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)):
a cell scrolled into view while its mark lasts is marked as it would have been had it been on screen,
as a cell's state in Excel does not depend on whether it was in view. Telling which version changed a
cell takes every version published within the highlight: with only the two newest, a change made two
redraws ago is no longer visible to the comparison. The alternative is to stamp a time on each cell as
its change is published. That costs in proportion to the cells whose shown text changed, which the
engine knows for a plain aggregate (the changed leaves and the totals above them); but a value shown as
a percentage of a total changes the shown text of every cell under that total, so every update would
format and compare them all. The Claude Code track measured that comparison over every row: a redraw at
401,001 rows went from 268.5 to 394.5 ms. Keeping the reports compares only the rows of the Window
served, when they are served, because a cell's shown value is computed when it is read
(`PivotReport.ValueAt`).

Rejected with it: **ag-grid's flash**, which keeps nothing — a rendered cell compares its old and new
value as its row changes, and a cell off screen at that moment never flashes
([research](../research/ag-grid-rendering-on-data-change.md)). It makes the mark depend on where the
user was looking. Rejected too: **a cap on the reports kept whatever the settings**, which ends marks
early, on screen as well, when redraws come faster than the cap allows.

The cost is said where it is chosen. The XML docs of `ChangeHighlightDuration` and `RedrawInterval` give
the bound: five reports at the defaults, twenty-one for a one-second highlight with a 50 ms
`RedrawInterval`. Data arriving faster than `RedrawInterval` is gathered into the next redraw, so it
adds no report. Running out of memory leaves a Stale Report.

**The time a change is marked is the component's own** *(settled while merging, 2026-10-08)*. A display
row carries, for each value cell, the Report Version its shown text last changed in
(`PivotDisplayRow.ChangedIn`), and the report's metadata lists the changes still being shown
(`ChangeMarks`). ExPivot stamps each listed change with its own `TimeProvider` when it first adopts a
report that lists it. As first built, the server stamped the time with its clock and the browser expired
the mark on its own: a server five seconds behind showed no mark at all
([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)'s note of
2026-10-08).

This refines ADR-0068's delegate change signal for ExPivot: a row's replacement carries its new
immutable state. An unchanged delegate with silently rewritten answers for an unchanged row
would still be wrong. Report Version changes alone do not replace rows, and versioned operations
still use the newest complete envelope or the version their gesture captured.

## A cancelled computation leaves the incremental state whole *(settled while merging, 2026-10-08)*

Every ExPivot gesture supersedes the question in flight, a scroll that needs a new Window among them. As
first built, a cancelled computation threw its incremental state away, so the next update read every
record again: over 100,000 records, a one-record update read 1 row, and the same update after one
cancellation read 150,002. Now cancellation is observed only in work built aside. Once the pending batches are folded in
place, the update runs to a whole state and is adopted at once; results computed and never published
are carried by the next Window Changes. A layout gesture after a discarded update lays out the cube the client
was last shown, so the next update marks the change.

One case keeps a residue, and it is accepted as ADR-0068's rule that a layout gesture marks nothing: when
the cube on screen cannot lay out the new layout (a field newly placed), or its Report Version was already
let go, the gesture brings the newest data with no mark for the change the user never saw. The values are
right.

## A first report costs its cube and its layout *(measured and fixed, 2026-10-09)*

A computation starts from what the cube build and the first layout made, which nothing writes after
them: an update writes only what it changes, under an overlay, and splits a node out of the first layout
only when it lays that node out again. Indexes that only an update needs are built when an update first
needs them. As first merged, all of that state was copied eagerly into per-entry immutable collections
and the report was laid out twice, so ticket 01's first report took 5.1 s at 401,001 rows against
`main`'s 0.6 s, with 1.0–1.3 s of collector pauses.

Now, on ticket 01's fixture (CoreCLR Release, tiered compilation off, a Linux container; medians):

| | As merged | Now | `main` |
|---|---|---|---|
| First report, 101,001 rows | 811 ms | 138 ms | 114 ms |
| First report, 401,001 rows | 5,140 ms | 816 ms | 613 ms |

The memory the session and its newest report hold halved, 647 to 325 MB after the first report. What
remains of the gap to `main` at 401,001 rows is about 0.1 s of computation setup and 0.15 s of label
sizing over every row. Live updates got faster with it, not slower (PV-48 records them). Nothing in the
design changed: the same results, the same incremental
behaviour, the same retention; the oracle test and `FirstReportStructureTests` hold it.

## Full refresh is an explicit source capability

**A Pivot Source unable to identify changes may explicitly declare that it refreshes the complete
aggregate result** (`PivotReportUpdateMode.FullRefresh`; a notice of newer data asks it to refresh,
settled while merging, 2026-10-08, when the declaration was found to be read by nothing, and confirmed by
the user on 2026-10-09). The user accepts this fallback for sources, such as an existing SQL query,
that can only announce that data changed. Partial-recomputation guarantees apply to the sources
that supply Change Batches or leaf changes. A full-refresh source must not silently claim them.

The bundled Snapshot source uses the incremental path. Both capabilities use the same
report semantics, complete-batch publication, version rules and Window transport. Even when
server computation refreshes in full, the browser receives the requested Window and its changes,
not the full set of leaves or rows. Failure retains a named Stale Report; recovery follows ADR-0152.

## The public boundary and lifetime

ExPivot consumes the same asynchronous report-source contract locally and remotely: report
metadata/Windows, Items, versioned Copy and Selection Summary, Details and Refresh. Direct engine
access is separate from the component's current metadata and Window. API names and old entry
points may change under ADR-0152; this does not change the package dependency direction.

Each report has its own computation state, independent of the Pivot Source's lifetime. The
Consumer manages server storage, sharing and disposal. Discarded state is recovered by requesting
a current Window; no permanent connection or indefinite version archive is required. Operations
against unavailable old versions refuse rather than silently switching data.

## Verification

Section 32 judges this ADR with LV-29 to LV-31 (LV-26 to LV-28 on the Codex track), LV-22's ExPivot
clause and PV-45; LV-24 to LV-28 judge the boundary it completes. LV-29 adds a cancellation at every
yield, and LV-30 server clocks behind and ahead. Test at the public engine/source boundary against
fresh computation, at the component boundary for render counts/retention/gesture correctness,
and in targeted real-browser scenarios. Repeat the recorded update-cost and memory experiments;
timings remain observations, never pass/fail thresholds. The diagnosed OOM is a functional failure.
