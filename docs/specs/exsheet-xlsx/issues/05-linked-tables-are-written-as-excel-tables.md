# 05: Linked Tables are written as Excel Tables on Sheets of their own

Status: ready-for-agent
Roadmap: ExSheet — A saved workbook carries each Linked Table as an Excel Table, so its Formulas keep working in Excel.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §6 (Q5, X1).

- [ ] Each Linked Table written as an Excel Table on a worksheet named after it, its rows as they stand
- [ ] Refused by name: a Linked Table not answered yet, more rows than 1,048,576, a name already a Sheet's
- [ ] Layer 1 tests; the Docs Site says the file reopens in Excel, not in ExSheet, until Excel Tables are read; Change Note

**Blocked by:** 04.
