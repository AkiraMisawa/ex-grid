# 04: Inserting and deleting rows and columns rewrites References on every Sheet

Status: ready-for-agent
Roadmap: ExSheet — Inserting a row on one Sheet keeps the Formulas on other Sheets pointing at the same cells.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) Consequences; ADR-0046 ("the engine belongs to the workbook").

- [ ] Row and column insertion and deletion on one Sheet rewrite References to it on every Sheet, as on its own
- [ ] Cut and paste (a move) and fill across Sheets rewrite as Excel does
- [ ] Copying between Sheets shifts relative References as Excel does
- [ ] An insertion or deletion on a Sheet of a Workbook of several is no longer refused with `NotSupportedException` (01)

**Blocked by:** 01.
