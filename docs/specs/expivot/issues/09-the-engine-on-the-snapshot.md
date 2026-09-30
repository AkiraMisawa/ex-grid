# 09: The engine on the Snapshot

Status: ready-for-agent

**What to build:** `ExPivot.Engine` rebuilt to aggregate a Snapshot's columns (ADR-0059, ADR-0063).

- **Only the parts the Value Fields ask for are accumulated.**
- **Integer and Decimal sums are exact**: scaled 64-bit integers, with `decimal` where needed and
  `double` only when an exact sum overflows.
- **The leaf index hashes well.** The first engine's `(row << 32) | column` key hashed to
  `row ^ column`, which made a large layout super-linearly slow: 23 s against 3.3 s with a mixing
  hash, at a million records.
- **Items come from the column's kind.** Text is folded ignoring case, and its label is the first
  spelling to arrive among the records present.
- **The Order Key**: a key per Item, with null keys last and a function that throws refused by name.
- **The date parts**: year, quarter and month declared from a Date column.
- **Pivot Fields are declared over Snapshot columns**, and a typed declaration builds both the
  column and the field.

Everything ADR-0059 already pinned stays pinned.

**Blocked by:** exgrid-data 01

- [ ] PV-3, PV-4 on the new engine; the existing layer-1 tests kept green or rewritten with their ADR
- [ ] PV-31: the Order Key's clauses
- [ ] PV-32: the date parts
- [ ] The leaf index hashes with mixing, with a test on a 270-by-12 layout that would collide under
  `lo ^ hi`

## Comments
