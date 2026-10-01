# 57: Every operation keeps the line between two cells one line

Status: ready-for-agent once the twelfth Windows run has answered

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"Readings until the twelfth Windows run". Setting an edge already writes both cells' sides (ticket 55).
A paste, Ctrl+D, Ctrl+R, the fill handle and deleting rows or columns still write each cell's own
sides, so the two records of one edge can disagree, and nothing says which was written later.

**Blocked by:** the twelfth Windows run, Part A, groups 1 and 2
(`docs/specs/exsheet/verify-on-windows-12.md`).

- [ ] **A paste, Ctrl+D, Ctrl+R and the fill handle** write the neighbour's side of each edge they
      write, and the later write wins, as the run found (cases 1–5). Undo puts back both sides.
- [ ] **Deleting rows or columns**: where two edges come together, the line kept is the one the run
      found (cases 6–9). The engine leaves one line, and both sides record it.
- [ ] **What ExSheet answers the core** when asked which of two recorded lines to draw (ADR-0050,
      item 15) follows the same rule, for documents written before this ticket.
- [ ] Layer 1, each test named with ADR-0063 and the run's case.
