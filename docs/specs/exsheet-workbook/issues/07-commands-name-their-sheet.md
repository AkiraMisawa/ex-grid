# 07: Commands name their Sheet, and the application manages Sheets

Status: ready-for-agent
Roadmap: ExSheet — An application's commands name the Sheet they act on, and it can add, rename, move, copy and delete Sheets itself.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §6.

- [ ] Commands and addresses take a Sheet; one naming none is refused by name while there are several Sheets
- [ ] Commands for each Sheet operation, and an event for each change; all on the one undo stack
- [ ] Layer 2 tests; Docs Site and Change Note

**Blocked by:** 03, 05.
