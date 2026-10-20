# 01: A Workbook computes References across its Sheets

Status: ready-for-agent
Roadmap: ExSheet — A Formula on one Sheet reads cells on another (`=Sheet2!A1`) and follows them as they change.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §9 and §2. The engine gains `Workbook` beside `Sheet` (expand): Sheets in order, one
culture, one dependency graph across them, Workbook-wide recalculation.

- [ ] A Workbook opens with Sheets, each named and holding Entries
- [ ] `=Sheet2!A1` computes from another Sheet, and recalculates when that cell changes, Values published once the whole recalculation completes
- [ ] A qualifier naming no Sheet of the Workbook is `#REF!`
- [ ] Linked Tables are declared on the Workbook and read from any Sheet
- [ ] Edits through the Workbook name their Sheet; a step undoes them
- [ ] Layer 1 tests at the `Workbook` seam, named for ADR-0331

**Blocked by:** None (can start immediately).
