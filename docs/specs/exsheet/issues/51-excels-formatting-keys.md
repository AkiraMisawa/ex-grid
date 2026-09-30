# 51: Excel's formatting keys

Status: ready-for-agent

**What to build:** [ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md) item 14, and
[ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Keys".

**Blocked by:** 50, and the eleventh Windows run's Part A (group 4) and Part B

- [ ] **Core: declared keys** (DC-57).
  - A declared key is claimed and raised together with whether an edit is open.
  - An undeclared key stays the browser's.
  - A declaration naming a key the core answers itself is refused by name.
- [ ] **ExSheet declares the keys the run found in Excel and in the page** (SH-42).
  - Each applies what Excel applies under the Sheet's culture, as one undo step.
  - A toggle follows the Focus cell (case 17).
  - Keys are read by character or by position as case 19 found.
- [ ] **Ctrl+1** opens Format Cells once ticket 52 is done. Until then it is not claimed.
- [ ] **While an edit is open**, a formatting key changes nothing (SH-43).
  - It is announced through the live region with its reason, and raised to the Consumer.
  - Ctrl+U is claimed, so the page's source does not open.
- [ ] Layer 2; Layer 3 for Ctrl+U and for the toggles.
