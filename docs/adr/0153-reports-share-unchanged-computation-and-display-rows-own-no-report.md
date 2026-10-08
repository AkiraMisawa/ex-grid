# Reports share unchanged computation, and display rows own no Report

*(Decided with the user, 2026-10-06, continuation Q8 and Q9: both recommendations accepted.
This completes the design in ADR-0150 to ADR-0152; implementation and verification follow.)*

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

## Historical display and Change Highlight remain correct

**Amended 2026-10-07:** [ADR-0154](./0154-user-writes-prevail-and-consumers-own-value-conflicts.md)
removes the displayed-text conflict policy. The historical-text paragraphs below describe the
previous requirement: their instruction not to shorten history no longer applies to value
history. Remove that history; retain only target/gesture evidence still needed under ADR-0154.
Detached ownership, immutable versions and the Change Highlight rules below remain current.


The grid's evidence records the text a historical paint compared, with detached identity and
position evidence where possible. A delayed gesture never recomputes its old value through a
mutable current report. Preserve ADR-0142's comparison, including the accessible number behind
`####`, the user's own writes and lost Action clicks. Do not shorten history or depend on when
GC runs. Required Action payloads and arbitrary Consumer Row Keys may still hold Consumer-owned
objects; this is not a promise of graph-free history for every possible Consumer.

ExPivot uses one stable Change Highlight lookup with immutable per-row change information, so
unchanged rows skip rendering. A new lookup is not created for every data version. A changed
raw value hidden by formatting still updates the semantic value without a false highlight.
Highlight expiry retains the grid's existing timer behavior. The required historical state is
bounded to its purpose and must not retain an unbounded chain of obsolete reports.

This refines ADR-0068's delegate change signal for ExPivot: a row's replacement carries its new
immutable state. An unchanged delegate with silently rewritten answers for an unchanged row
would still be wrong. Report Version changes alone do not replace rows, and versioned operations
still use the newest complete envelope or the version their gesture captured.

## Full refresh is an explicit provider capability

**A provider unable to identify changes may explicitly declare that it refreshes the complete
aggregate result.** The user accepts this fallback for sources, such as an existing SQL query,
that can only announce that data changed. Partial-recomputation guarantees apply to the sources
that supply Change Batches or leaf changes. A full-refresh provider must not silently claim them.

The bundled Snapshot source uses the incremental path. Both provider capabilities use the same
report semantics, complete-batch publication, version rules and Window transport. Even when
server computation refreshes in full, the browser receives the requested Window and its changes,
not the full set of leaves or rows. Failure retains a named Stale Report; recovery follows ADR-0152.

## The public boundary and lifetime

ExPivot consumes the same asynchronous report-source contract locally and remotely: report
metadata/Windows, Items, versioned Copy and Selection Summary, Details and Refresh. Direct engine
access is separate from the component's current metadata and Window. API names and old entry
points may change under ADR-0152; this does not change the package dependency direction.

Each report has its own computation state, independent of the data provider's lifetime. The
Consumer manages server storage, sharing and disposal. Discarded state is recovered by requesting
a current Window; no permanent connection or indefinite version archive is required. Operations
against unavailable old versions refuse rather than silently switching data.

## Verification

LV-20 to LV-25 remain required. LV-26 to LV-28 add incremental dependency coverage, unchanged-row
rendering and explicit full-refresh behavior. Test at the public engine/source boundary against
fresh computation, at the component boundary for render counts/retention/gesture correctness,
and in targeted real-browser scenarios. Repeat the recorded update-cost and memory experiments;
timings remain observations, never pass/fail thresholds. The diagnosed OOM is a functional failure.
