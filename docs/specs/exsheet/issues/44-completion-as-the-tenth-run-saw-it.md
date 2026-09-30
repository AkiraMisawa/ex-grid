# 44: Completion as the tenth Windows run saw it

Status: ready-for-agent

**What to build:** ADR-0058, "What the tenth Windows run settled", and ADR-0051's correction of the
same day. The tenth run (`verification/2026-09-30-windows-excel-10/excel-only.md`, group 1)
contradicted the readings ticket 39 was built on.

**Blocked by:** None (ticket 39 is merged). Its script change shares `ex-grid.js` with ticket 35;
start after 35 is merged, or keep the change to the gate and name it.

- [ ] A value typed whole lists that value alone, selected (`0` → `0 - Exact match`). Any other text
      at a value-list argument lists every value, the first selected (`-` → all five, `0` selected)
      (SH-36)
- [ ] Tab closes the list, after a value, a column or a table's name, and the grid does not ask
      again for a list on the text Tab wrote (today it reopens on the accepted name) (SH-36)
- [ ] → at an open value list, where the caret stands at a Reference's place, points and closes the
      list: `=XLOOKUP(1,A2:A4,B2:B4,,` then → writes `E10`, shown selected. In a list of names, ← and →
      still move the caret. The key listener's gate learns that a list is open over Point; no new
      listener, no layout read (ADR-0021) (SH-36)
- [ ] ↑, ↓, Tab and Escape at an open list are unchanged (SH-36)
- [ ] Layer 1 for the listing rule; Layer 2 for Tab closing and → pointing; Layer 3 on `/sheet` under
      both Chromes for → pointing from an open value list and Tab closing (SH-36)
