# 04: The Record Key and the Change Batch

Status: done

**What to build:**

- **The Record Key**: one declared column, Text or Integer. Two records under one key are refused,
  naming the key.
- **The Change Batch**: the records added, the records changed (by key), and the keys removed. It
  is applied whole into the next Snapshot, or refused whole.
- **The next Snapshot** shares every segment the batch did not touch.
  - A changed record keeps its place in the order, and an added one goes at the end.
  - A dictionary only grows, so a code means the same text in every version.
- **A Snapshot without a key** takes only batches that add.
- **What a reader can fold in:** it is handed what a batch removed and what it added, so that it can
  update what it computed rather than start again (ADR-0067).

**Blocked by:** 01

- [x] DA-11: every clause a named test
- [x] DA-12: applying 1,000 changes to 1,000,000 records allocates in proportion to the changes
- [x] DA-2: the Snapshot before a batch reads exactly as before

## Comments

Built, 2026-10-01, with ticket 01.

- **`ChangeBatch.Of(added, changed, removedKeys)`** takes small Snapshots, so any way in makes a
  batch; `SnapshotBuilder<T>.Batch` makes one from records. A removed key is a `string` or any
  integer type, held as a `long`. `Snapshot.Apply` checks every rule before it makes anything, and
  refuses by key (`SnapshotException.Key`): a changed or removed key not held, an added key held,
  a key twice in one role, a key both changed and removed, a Blank key (by its row in the batch),
  a removed key of the other kind. A key removed and added in one batch is a new record at the end.
  The batch's columns are matched to the Snapshot's by name and kind; a Snapshot that keeps its
  records takes only a batch whose records are kept and of the same type, and one that keeps none
  ignores a batch's.
- **The next Snapshot** shares every segment, and adds one (or a few) holding the changed and
  added records, each row carrying its position: a changed record the one it replaces, an added
  record the next past every position so far. The rows it no longer holds are masked in removal
  sets of its own — sorted offsets while few, a bit set once many — so a batch copies only the sets
  of the segments it touches. `Rows` is placed by position on first read, linear in the positions.
  A text dictionary is shared by every version and only grows; two versions made from one fork it,
  sharing what lies below their common count.
- **`SnapshotChange`**: `Before`, `After`, `Removed` (rows of Before that After does not hold) and
  `Added` (rows of After that Before did not hold), and `Compacted`. Compaction keeps values, codes
  and order, and moves addresses: once 64 slices made by batches pile up, the rows they still hold
  are merged into as few slices as hold them, sharing the base; once the rows batches made pass a
  quarter of the base, or most stored rows are dead, the whole is copied into a new base. A reader
  that keeps rows by address starts again when `Compacted` is true.
- **Measured** (4 vCPUs, .NET 10.0.12, CoreCLR, never gated): 1,000 changes (500 changed, 250
  added, 250 removed) to a million records apply in 0.8–0.9 ms once warm and allocate about 115 KB
  with six columns (133 KiB with eleven), the same at ten thousand records as at a million. The first read of `Rows` after a batch takes
  about 6 ms. A merge of the slices 64 batches of 750 changes made takes 18–31 ms; a whole
  compaction of 1.3 million rows, about 0.17 s.
