# 11: 3D References

Status: ready-for-agent
Roadmap: ExSheet — `=SUM(Sheet1:Sheet3!B2)` adds the same cell across a span of Sheets, as Excel does.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §7.

- [ ] The parser and the engine read `First:Last!Range` over the Sheets in tab order
- [ ] Only the functions Excel accepts one in take it, among those ExSheet supports; any other answers as Excel does
- [ ] Moving, inserting and deleting Sheets inside or at the ends of the span changes it as ticket 12 read from Excel
- [ ] Layer 1 cases from Excel's readings

**Blocked by:** 03, 12.
