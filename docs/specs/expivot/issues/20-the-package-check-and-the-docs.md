# 20: The package check and the docs

Status: done

**What to build:**

- **The package check:** `ExGrid.Data` and `ExGrid.Data.Arrow` are packed into the pivot feed, the
  references are read back, and an Arrow stream is read and written through the packed packages.
- **The docs:** the packages' READMEs, `docs/implementation-status.md`, the root README, and
  `tests/ExGrid.Browser/README.md`.

**Blocked by:** 19

- [x] PV-1, DA-1, DA-16

## Comments

2026-10-01: The package check holds `ExPivot.Engine` to exactly the `ExGrid.Data` it was built with
(it said "depends on nothing at all" until the engine was rebuilt on the Snapshot), and every other
package the family packs to referencing no data package. It now also compiles the engine README's
standard examples — typed declarations, `PivotSource.From`, a report asked of a source, a Change
Batch — against the packed package. Passed.
