# 11: Live data in the bundled source

Status: ready-for-agent

**What to build:** `PivotSource.From(snapshot)` takes a Change Batch (ADR-0066). It makes the next
Snapshot, and folds the batch into the answer it holds for the current question.

- **Exact parts are updated by subtraction and addition.**
- **Every other part is recomputed for the leaves the batch touched.**
- **Leaves come and go with their Items.**
- **`Changed` is raised** with the new Source Version.

**Blocked by:** 10, exgrid-data 04

- [x] PV-34: a property test applies random batches, and every leaf equals a fresh aggregation, to
  the last bit
- [x] An Item that appears or leaves in a batch, as a named test

## Comments

2026-10-01: Built: `SnapshotPivotSource.Apply(batch)`. The Snapshot applies the batch, whole or
not at all; the pass held for the current question is folded, then `Changed` is raised with the
new Source Version (the source's own id and the Snapshot's version).

- **Removed rows**, read in Before: their leaf's records, counts and Integer or Decimal sum are
  taken away — integers, so the order cannot matter; a leaf whose other parts they touched is
  marked. Text codes are counted out, so a spelling leaves with its last record.
- **Added rows** are the batch's slices, read as a fresh pass reads any slice: a new Item brings
  its leaves.
- **Marked leaves are recomputed** from their own rows, in slice order, as a fresh pass computes
  them: along a chain of each leaf's rows (made the first time it is needed), or by one sweep
  over the column when they hold more than one row in sixteen. A leaf whose exact sum has passed
  128 bits is recomputed whenever a batch touches it.
- **A compaction** moves rows, so the held pass is dropped and the next question is answered
  afresh, which rebuilds the row index; so is one that would pass `MaxLeaves`.
- **Versions held**: the current one and those of the source's last four answers
  (`SnapshotPivotSource.AnswersHeld`); an older one is refused with `SourceVersionNotHeld`.

`LiveDataTests` applies 90 random batches per seed, five seeds — adds, changes, removes; text in
several spellings; money at new scales, past 64 bits and near decimal's edge; non-finite
doubles; Items appearing and leaving — to eight questions, and holds every leaf of each folded
answer to a fresh aggregation, to the last bit; it fails when the recompute or the sweep is
broken.

Measured (as ticket 09's comment; 1,000 changes = 800 changed, 100 added, 100 removed, on a
million records; median of 30 batches): exact Sums, apply and fold 5.8 ms, then the answer
1.0 ms; a double's Sum 15.1 ms; Max of Notional 15.0 ms; 270 dates with exact Sums 4.4 ms, then
the answer of 199,511 leaves 23 ms. Building the batch 0.3 ms.
