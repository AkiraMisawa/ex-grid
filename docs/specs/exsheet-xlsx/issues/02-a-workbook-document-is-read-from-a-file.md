# 02: A Workbook Document is read from a file, with its Open Report

Status: ready-for-agent
Roadmap: ExSheet — An `.xlsx` opens as a Workbook, its Values computed again, with a report of what was not carried.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §3, §4, §5 and §7.

- [ ] A new shipped package `ExSheet.Xlsx` (refs `ExGrid.Data.Xlsx`, `ExSheet.Engine`), packed and smoke-tested
- [ ] Every worksheet a Sheet, in order, the shown one shown; constants and Formulas carried, `_xlfn.`/`_xlws.` taken off, shared formulas expanded, spilled arrays as their anchor's Formula; 1904 serials moved to 1900's
- [ ] The culture taken from the caller
- [ ] Refused by name with Sheet and cell: an unreadable Formula, a defined name, another workbook, an Excel Table reference, a legacy array formula
- [ ] The Open Report: what was not carried, and cells naming unsupported functions
- [ ] `CONTEXT.md` gains Open Report; layer 1 tests; Change Note

**Blocked by:** 01.
