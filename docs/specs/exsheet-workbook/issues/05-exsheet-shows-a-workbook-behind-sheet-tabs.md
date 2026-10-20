# 05: ExSheet shows a Workbook behind Sheet Tabs

Status: ready-for-agent
Roadmap: ExSheet — An ExSheet shows a tab for each Sheet at its foot and switches between them with a click or Ctrl+PageUp/PageDown.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §1, §2, §4 (switching). The component moves onto `Workbook`.

- [ ] A `Workbook` parameter and its change event; `Document` still works as a Workbook of one Sheet; both set is refused by name
- [ ] One ExGrid draws the Sheet shown; P1 to P9 hold with several Sheets
- [ ] A tab per Sheet (Built-in Chrome), a tab list to assistive technology; a click and Ctrl+PageUp/PageDown switch, each an input in the Input Order
- [ ] Each Sheet's selection, active cell, scroll offsets and column widths are restored when it is shown again
- [ ] Layer 2 for the parameters and view state; layer 3 for the keys and a key typed at once after a switch
- [ ] Docs Site: the Workbook page and Example; Change Note

**Blocked by:** 02.
