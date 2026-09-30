# 11: Live data in the bundled source

Status: ready-for-agent

**What to build:** `PivotSource.From(snapshot)` takes a Change Batch (ADR-0066). It makes the next
Snapshot, and folds the batch into the answer it holds for the current question.

- **Exact parts are updated by subtraction and addition.**
- **Every other part is recomputed for the leaves the batch touched.**
- **Leaves come and go with their Items.**
- **`Changed` is raised** with the new Source Version.

**Blocked by:** 10, exgrid-data 04

- [ ] PV-34: a property test applies random batches, and every leaf equals a fresh aggregation, to
  the last bit
- [ ] An Item that appears or leaves in a batch, as a named test

## Comments
