# 01: A Workbook computes References across its Sheets

Status: done
Roadmap: ExSheet — A Formula on one Sheet reads cells on another (`=Sheet2!A1`) and follows them as they change.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §9 and §2. The engine gains `Workbook` beside `Sheet` (expand): Sheets in order, one
culture, one dependency graph across them, Workbook-wide recalculation.

- [x] A Workbook opens with Sheets, each named and holding Entries
- [x] `=Sheet2!A1` computes from another Sheet, and recalculates when that cell changes, Values published once the whole recalculation completes
- [x] A qualifier naming no Sheet of the Workbook is `#REF!`
- [x] Linked Tables are declared on the Workbook and read from any Sheet
- [x] Edits through the Workbook name their Sheet; a step undoes them
- [x] Layer 1 tests at the `Workbook` seam, named for ADR-0331

**Blocked by:** None (can start immediately).

**Settled while building (2026-10-20):** a Sheet created on its own belongs to a Workbook of that one
Sheet, so every recalculation runs across a Workbook. Until 03 and 04 rewrite other Sheets' References,
a rename and an insertion or deletion on a Sheet of a Workbook of several throw `NotSupportedException`
rather than leave those References stale. The Sheet Seed, the count of changes, `NowSource` and the
Sheet Day stay on each Sheet until 13 moves them to the Workbook: a change on one Sheet recomputes the
volatile Formulas of every Sheet, but `RAND` on another Sheet draws again only when that Sheet changes.
