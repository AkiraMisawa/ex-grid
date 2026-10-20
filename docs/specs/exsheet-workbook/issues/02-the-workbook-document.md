# 02: The Workbook Document

Status: ready-for-agent
Roadmap: ExSheet — An application saves and opens every Sheet of a Workbook as one document.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §2. A format of its own with a version; a Sheet Document opens as a Workbook of one Sheet.

- [ ] `Workbook` writes and opens a Workbook Document: Sheets in order, the Sheet shown, the culture, the Linked Tables, each Sheet's Entries, Cell Formats and column widths, never a Value
- [ ] An unknown version, or anything not understood, is refused by name
- [ ] A Sheet Document of every readable version opens as a Workbook of one Sheet
- [ ] `CONTEXT.md`'s Workbook Document entry holds

**Blocked by:** 01.
