# 03: Adding, renaming, moving, copying and deleting a Sheet are steps

Status: done
Roadmap: ExSheet — Sheets are added, renamed, moved, copied and deleted, and each can be undone, a deletion included.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §3, §4 (names), §6.

- [x] Each operation is one `SheetEdit` of the Workbook and returns the step that undoes it
- [x] A new Sheet takes the next free `SheetN`; a copy takes `Name (2)`
- [x] Excel's name rules: 31 characters, none of `[ ] : * ? / \`, not empty, unique without regard to case; a breach is refused by name
- [x] A rename rewrites every Formula that names the Sheet
- [x] A deletion turns References to the Sheet into `#REF!`; undoing it gives back the Sheet and those Formulas exactly
- [x] Moving changes the order; copying copies Entries, formats and widths
- [x] A rename on a Sheet of a Workbook of several is no longer refused with `NotSupportedException` (01)

**Blocked by:** 01.

**Settled while building (2026-10-20):** the operations are `SheetEdit.InsertSheet`, `Rename`,
`MoveSheet`, `CopySheet` and `DeleteSheet`, given to `Workbook.Do`; each recalculates the whole
Workbook once, as a Formula whose qualifier may now name another Sheet (or none) reads again. Deleting
the last Sheet is refused (`LastSheet`); a name taken is `SheetNameTaken`. A deleted Sheet's References
become a plain `#REF!`, its qualifier dropped; a copy's References to its source name the copy. Both
are asked of Excel in 12. Structural insertions and deletions on a Sheet of several stay refused until 04.
