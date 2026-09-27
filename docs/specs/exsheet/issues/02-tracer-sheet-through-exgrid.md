# 02: Tracer: a Sheet typed into and drawn through ExGrid

Status: ready-for-agent

**What to build:** The thinnest complete path. The `ExSheet.Engine` and `ExSheet` packages exist, and a DemoHost
page shows a Sheet. A user clicks a cell, types `2` into A1, `3` into B1 and `=A1+B1` into C1, and
sees 5. ExSheet renders one ExGrid, with Columns `A`…`XFD` that ExSheet builds, `TotalCount`
1,048,576, rows as positions, and a new row instance for each row whose Values changed. The
Consumer receives a Sheet Document when the Sheet changes, and can hand one in. The arithmetic is
only `+ - * /` over single-cell References. The rest of the grammar is ticket 03.

**Blocked by:** None (can start immediately)

- [ ] `ExSheet.Engine` references nothing of ours; `ExSheet` references it and `ExGrid`; nothing references `ExSheet` (ADR-0046/0047)
- [ ] Typing a constant and a Formula shows the Value; editing A1 updates C1 and repaints only the rows whose Values changed (layer 2, render counts)
- [ ] Scrolling reaches row 1,048,576 and column XFD, and the DOM does not grow with the extent
- [ ] A row height at which 1,048,576 rows exceed the scroll ceiling is refused by name (ADR-0046, VZ-8)
- [ ] Pinned Columns work on a Sheet
- [ ] The Sheet Document round-trips: out, in, the same Values (ADR-0048)
- [ ] ExGrid's sort and filter are not wired on a Sheet
- [ ] A DemoHost page shows it on both hosts, with a clean console

## Comments
