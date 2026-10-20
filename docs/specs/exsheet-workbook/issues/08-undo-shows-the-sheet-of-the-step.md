# 08: Undo and Redo show the Sheet their step was taken on

Status: ready-for-agent
Roadmap: ExSheet — Undo shows the Sheet the undone change was made on and selects what it changed.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §3.

- [ ] Ctrl+Z and Ctrl+Y show the step's Sheet and select its range, as Excel does
- [ ] A Sheet operation's undo shows the Sheet it brings back or the one it leaves
- [ ] Layer 2 and layer 3

**Blocked by:** 06.
