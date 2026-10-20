# 09: Pointing reaches across Sheets, and the Name Box goes to another Sheet

Status: ready-for-agent
Roadmap: ExSheet — While typing a Formula, pressing another Sheet's tab and pointing at a cell writes `Sheet2!A1`.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §5; ADR-0058's Pointing Scope widened to the Workbook.

- [ ] A tab press or Ctrl+PageUp/PageDown while a Formula is typed shows that Sheet and keeps the edit; pointing writes the qualified Reference; Enter commits on the first Sheet and shows it
- [ ] What a tab press does while a constant is typed follows ticket 12's reading
- [ ] The Name Box takes `Sheet2!B5`
- [ ] Layer 3 on both hosts; Docs Site and Change Note

**Blocked by:** 05, 12 (the constant case only).
