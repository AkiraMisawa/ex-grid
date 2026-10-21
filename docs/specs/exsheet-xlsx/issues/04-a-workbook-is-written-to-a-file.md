# 04: A Workbook is written to a file

Status: ready-for-agent
Roadmap: ExSheet — A Workbook saves as an `.xlsx` that Excel opens and recalculates, with the Values the user saw.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §6.

- [ ] Written from the Workbook as it stands: every Sheet, each Formula with its Value, prefixes put back, Cell Formats and widths
- [ ] Marked for full calculation on load
- [ ] The cost of formatted rows crossing formatted columns bounded or settled: ticket 03's `SheetFormatPlan` writes a cell
      wherever a formatted row crosses a formatted column with a format of its own, so a Sheet with many of each writes
      rows × columns cells, unbounded today
- [ ] Open, save, open again keeps everything §4 carries
- [ ] Layer 1 tests; Change Note

**Blocked by:** 03.
