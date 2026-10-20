# 03: Adding, renaming, moving, copying and deleting a Sheet are steps

Status: ready-for-agent
Roadmap: ExSheet — Sheets are added, renamed, moved, copied and deleted, and each can be undone, a deletion included.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §3, §4 (names), §6.

- [ ] Each operation is one `SheetEdit` of the Workbook and returns the step that undoes it
- [ ] A new Sheet takes the next free `SheetN`; a copy takes `Name (2)`
- [ ] Excel's name rules: 31 characters, none of `[ ] : * ? / \`, not empty, unique without regard to case; a breach is refused by name
- [ ] A rename rewrites every Formula that names the Sheet
- [ ] A deletion turns References to the Sheet into `#REF!`; undoing it gives back the Sheet and those Formulas exactly
- [ ] Moving changes the order; copying copies Entries, formats and widths

**Blocked by:** 01.
