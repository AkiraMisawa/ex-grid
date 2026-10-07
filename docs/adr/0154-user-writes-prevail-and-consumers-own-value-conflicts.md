# User writes prevail, and Consumers own value conflicts

*(Decided with the user, 2026-10-07. Both recommendations were accepted: apply the same
policy to editing, paste, Delete and fills, and let the Consumer judge an Action's business
conflicts. This deliberately replaces ADR-0142's displayed-text conflict policy.)*

**A change of values alone does not make the grid refuse the user's operation.** If a user
saw 100, upstream changed the same cell to 200, and the user commits 150, the grid raises an
Edit Intent for 150 against the current target. Consumer validation still applies. This rule
concerns the operation when handled; it does not freeze the cell against later data updates.
The grid continues to notify rather than own or execute the Consumer's writes.

## One policy for every write

Cell Editor commits, paste, Ctrl+Enter, Delete, Ctrl+D, Ctrl+R and fill-handle drags do not
compare old displayed text with current text. A fill source changing value alone does not
refuse the fill either. Existing source acquisition and Fill Intent semantics stay in force:
Ctrl+D/R acquire the source values under the current row sequence, and the fill handle sends
its source/target ranges for the Consumer to interpret. No new snapshot of old displayed
values is implied. The Consumer applies an accepted operation, including any business-specific
conflict check. A Consumer rejection is still presented through its existing verdict/refusal
path; the grid never claims a rejected write succeeded.

Before constructing a write's current-row intent, a bound source still publishes gathered
changes. This prevents an edited cell from restoring old values in unrelated columns. The
source's protection against replacing a row using an obsolete instance remains: it is separate
from the removed displayed-text check.

## Target identity and operation rules remain

The grid must still know which row, column or range the user acted on. Row Sequence Version,
captured layout, editable-column declarations, selection/shape/size rules, source acquisition
completeness and Consumer validation retain their existing semantics. A delayed operation is
never silently redirected to another row or column. A reordering or loss of the edited row
still discards the editor with its existing named reason (ADR-0011); a stale range is refused.
A stable Row Key does not newly make a positional edit follow a reordered row.

**Implementation clarification, 2026-10-07:** source-local sequence counters may be reused by a
replacement Source. An operation captured under the old binding keeps a detached binding
identity and refuses if that binding is replaced before delivery. This makes the original-target
rule concrete when two sources both report sequence zero. Ordinary Selection rebinding and new
gestures on the new Source are unchanged; publications by the same Source remain governed by
the value-only rule above. An open Cell Editor or Formula Bar edit is likewise bound to its
original Source. Replacing that Source discards the edit with `EditDiscardReason.SourceChanged`,
while ordinary Selection rebinding is preserved. This added reason describes the actual loss of
the target; reporting a row reordering would be false when both sources have sequence zero.

An Action names its original row and command. With a Row Key, it resolves to the current row
under that key, including after reordering. Without one, the existing instance/unchanged-order
position evidence must establish the target. A missing or unidentifiable target is refused.
The Action reaches its Consumer once even when Blazor disposes the original row component
before delivering the click. No displayed-value comparison guards that dispatch: the Consumer
knows whether a command needs a price, version, permission or other business-specific check.

## Retain evidence for its purpose

Remove the grid's historical cell-text snapshots, editor baseline text, and bookkeeping that
exempted the user's own writes from comparing that text. They no longer implement a requirement.
Do not keep a 64-paint value history on display-only grids. Bounded target/address evidence
needed to identify an Action and deliver its click once may remain where Actions exist; it
must not preserve report-sized graphs merely to remember a rendered cell. Layout/gesture
ordering evidence has a separate purpose and is not removed with value history.

This does not remove ExPivot's Report Versions for Copy, Summary or Details, nor its timed
Change Highlight evidence. Detached display rows/keys, immutable published versions and
incremental computation from ADR-0153 remain necessary. Their correctness must not depend on
GC timing. The earlier retention fix was required under the previous policy; changing the
policy is not evidence that the remaining high-cardinality memory footprint is now small.
Measure both allocation and sustained browser memory after implementation, keeping source
record count distinct from report cardinality.

## Why the decision changed

ADR-0142 initially made the core protect unseen upstream value changes, even for arbitrary
Actions. Implementing that policy required historical displayed-text evidence and special
handling of a user's own writes. The user now prefers the explicit operation to prevail and
business conflict rules to belong to the Consumer. A general-purpose grid cannot infer that
an Action to cancel a record needs the same check as an Action to accept a quoted price.

AG Grid release-36.2.0 was read at the pinned commit, without copying Enterprise code. Its
normal [commit path](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/edit/editService.ts#L921-L950)
writes the edit to the same RowNode/column; its [setter](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/valueService/valueService.ts#L758-L804)
sets the input value. This is not a claim that every AG Grid edit always wins: validation,
custom setters and readOnlyEdit can defer or refuse it. ExGrid keeps its own immutable,
Consumer-owned write model.

## Verification

Replace old automatic-conflict expectations with accepted-operation tests across the public
component and browser seams. Cover edits while upstream changes, every range-write family,
Actions with and without Row Keys, reordering/removal, gathered updates, Consumer Reject,
held keys and clipboard waits. Preserve deterministic no-lost/no-duplicate Action delivery.
Check that display-only ExPivot no longer retains grid value history and repeat the large
browser fixture with normal GC and unchanged caps. Timings remain observations, never gates.
