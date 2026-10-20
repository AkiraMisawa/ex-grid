# 06: The Sheet Tabs add, rename, move, copy and delete

Status: ready-for-agent
Roadmap: ExSheet — Sheets are added, renamed, moved, copied and deleted from their tabs, by pointer or keyboard alone.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §4.

- [ ] `+` and Shift+F11 add; a double click renames on the tab (Enter commits, Escape cancels); a tab's menu deletes, moves left/right and copies; a drag moves
- [ ] Every one reachable from the keyboard, announced in the live region
- [ ] Each is an input in the Input Order and one step on the undo stack
- [ ] Layer 3 at machine speed on both hosts
- [ ] Docs Site and Change Note

**Blocked by:** 03, 05.
