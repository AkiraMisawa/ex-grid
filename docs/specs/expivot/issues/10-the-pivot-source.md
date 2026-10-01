# 10: The Pivot Source and its JSON

Status: done

**What to build:** `PivotSource` (ADR-0065).

- **Its members:** `Fields`, `Features`, `AggregateAsync`, `ItemsAsync`, `DetailsAsync`,
  `RefreshAsync` and `Changed`.
- **The questions and answers:**
  - `PivotQuery`: the rows, the columns, the Hidden Items of every placed field, the parts per Value
    Field's field, and `MaxLeaves`.
  - `PivotAnswer`: the Source Version, each axis field's Items, and the leaves; or a refusal.
  - `PivotItemsQuery` and `PivotItemPage`.
  - `PivotDetailsQuery` and `PivotDetailPage`.
- **`PivotJson`**, with a version in every document.
- **The sources:**
  - `PivotSource.From(snapshot)`, which works in slices and cancels at a slice;
  - `PivotSource.From(records, fields)`;
  - `PivotSource.Fetch(fields, features, aggregate, items, details)`, with a way for the Consumer to
    say that its data changed.

**Blocked by:** 09

- [x] PV-16: `PivotJson` round-trips every message and refuses an unknown version
- [x] PV-22: `Fetch` over a `From` on the same Snapshot answers identically
- [x] PV-23: Items and Details under the answer's version; a source that cannot answer refuses
- [x] PV-27: slices, and cancellation at a slice
- [x] PV-29: `MaxLeaves` refused, with the bound

## Comments

2026-10-01: The contract is built over records: `PivotSource`, the questions and answers,
`PivotJson`, `PivotSource.From(records, fields)` in slices, `PivotSource.Fetch`, and the report
laid out from Leaf Aggregates, with the cell index's hash fixed (8.0 s → 1.2 s for 270 dates in
Columns at a million records). PV-16, PV-22, PV-23, PV-27 and PV-29's engine side pass. What
remains for this ticket is `From(snapshot)`, which ticket 09 brings, and leaving Filters fields
that hide nothing out of a question (ADR-0065, refined).

2026-10-01: `PivotSource.From(snapshot, fields?)` built (`SnapshotPivotSource`). With no fields,
one per column, captioned as the column is and typed by its kind; declared fields name their
columns, by their own name or `PivotField.Column`, and an unknown column is refused by name.
It answers Aggregate, Items and Details from the Snapshot in slices, cancellable at a slice,
refusing `MaxLeaves` at the row that passes it. `Fetch` over JSON answers as it does, before
and after Change Batches (`SnapshotSourceTests`). A field in Filters that hides nothing makes no
`ItemSpace` in the pass; leaving it out of a question (`PivotQuery.For`, `PivotCube.Holds`) is
the component's change.
