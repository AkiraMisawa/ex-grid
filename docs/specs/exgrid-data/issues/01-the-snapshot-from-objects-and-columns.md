# 01: The Snapshot, built from objects and from columns

Status: ready-for-agent

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

- [ ] DA-2: no public member mutates a Snapshot
- [ ] DA-3: each kind holds its values as ADR-0063 says, with a Blank apart from `""` and 0
- [ ] DA-4: typed accessors box nothing, and the objects are kept by reference, in order
- [ ] DA-5: cancellation, progress and slices
- [ ] DA-6: an unreadable value fails the build, naming the row and the column
- [ ] Added to `ExGrid.slnx`, and to the package check's feed with ExPivot (DA-1)

## Comments
