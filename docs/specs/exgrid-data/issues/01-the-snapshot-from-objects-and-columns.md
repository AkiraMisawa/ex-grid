# 01: The Snapshot, built from objects and from columns

Status: done

**What to build:**

- A new project, `src/ExGrid.Data`, with no package dependency. It targets `net10.0` and documents
  every public member.
- Its tests, `tests/ExGrid.Data.Tests`.
- **The Snapshot**:
  - its columns of the six kinds, with Blanks;
  - Text as a dictionary in order of first appearance;
  - Decimal as scaled 64-bit integers, or as `decimal` where a value does not fit;
  - Date as clock ticks;
  - the objects kept when it is built from objects;
  - a version.
- **A builder from objects with typed accessors**, and an untyped accessor under a declared kind.
- **A builder from columns.**

Both builders work in slices, take a `CancellationToken`, report progress, and fail whole, naming
the row and the column. Storage is in segments, so that ticket 04 can share them.

**Blocked by:** None

- [x] DA-2: no public member mutates a Snapshot
- [x] DA-3: each kind holds its values as ADR-0063 says, with a Blank apart from `""` and 0
- [x] DA-4: typed accessors box nothing, and the objects are kept by reference, in order
- [x] DA-5: cancellation, progress and slices
- [x] DA-6: an unreadable value fails the build, naming the row and the column
- [x] Added to `ExGrid.slnx`, and to the package check's feed with ExPivot (DA-1)

## Comments

Built, 2026-10-01, with ticket 04.

- **`Snapshot`** holds its rows in **segments** of 65,536, one array per column each, read as
  **slices**: `Slice(i).Codes / Decimals / Doubles / Integers / Ticks / Booleans / Blanks / Removed`
  are spans, so a whole column is a loop over each slice's span. A Blank's slot holds the kind's
  zero (code -1 for Text), and every kind, Text included, hands out its Blanks as a bit set.
  `Rows` lists the rows a version holds in order, indexed in constant time; `ValueAt`, `IsBlank`,
  `RecordAt` and `Holds` are for small reads.
- **Decimal** is chosen per segment, as the brief asked: scaled longs at the largest number of
  places among the segment's values, trailing zeros not counted, or `decimal` when one does not fit.
  A value reads back without trailing zeros, so `1.5` and `1.50` read back alike.
- **From objects**, `SnapshotBuilder<T>`: typed accessors (`int?` as well as `long?` for Integer;
  `DateTime?`, `DateOnly?` and `DateTimeOffset?` for Date) and `Column(name, kind, Func<T, object?>)`.
  The untyped accessor takes exact conversions only — any integer type for Decimal and Integer, a
  `float` for Double, the three date types for Date; `null` and `DBNull` are Blanks. Under Text,
  any value that is not a string is its invariant text (ADR-0059, as the orchestrator decided:
  an enum by its name, a `Guid` in its D form). Anything else fails the build by row and column.
  A records list that changes its count while an asynchronous build runs is refused.
- **From columns**, `SnapshotColumnsBuilder`: a `ColumnBuilder` per column, appended a value or a
  span at a time; a bulk append takes Blanks as a bit set in the slices' own form. Text also comes
  as a `ReadOnlySpan<char>` or UTF-8 bytes (a string is made only for text not yet held), or as
  codes into another producer's dictionary, taken under the Snapshot's rules (first appearance in
  the rows, one entry per text, a null entry a Blank, a code outside the dictionary refused by row
  and column). Decimal also comes scaled (`AppendScaled`), Date as ticks (out of range refused).
  `Version` restores a written Snapshot's version. A reader paces the load with `CheckpointAsync`
  (cancellation, progress with rows and bytes, a yield when the slice is spent); `BuildAsync`
  indexes the Record Key in slices. A refused load builds nothing, and a builder builds once.
- **Slices**: `SnapshotLoadOptions.SliceBudget` (30 ms by default) and `Yield` (by default
  `Task.Yield()`, and `Task.Delay(1)` in a browser — *changed 2026-10-01 to `Task.Yield()` there
  too*: ticket 07 measured it painting a frame a slice at 0.4–0.6 ms a yield, against the delay's
  4.3–4.4 ms, about 0.4 s of a million-row CSV read).
- **Package check**: `ExGrid.Data` is packed into the ExPivot feed, read back as declaring no
  dependency, refused in the release feed, and restored by the smoke application, which compiles
  its README's examples. Run here step by step and green.
- **Measured** (4 vCPUs, .NET 10.0.12, CoreCLR, never gated): a million records of eleven columns
  built from objects in 0.16–0.18 s once warm (0.43 s cold), 78 MiB, nothing transient; a whole
  column scanned in 0.4–3 ms.
