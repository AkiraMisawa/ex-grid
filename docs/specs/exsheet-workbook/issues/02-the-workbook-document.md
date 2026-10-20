# 02: The Workbook Document

Status: done
Roadmap: ExSheet — An application saves and opens every Sheet of a Workbook as one document.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §2. A format of its own with a version; a Sheet Document opens as a Workbook of one Sheet.

- [x] `Workbook` writes and opens a Workbook Document: Sheets in order, the Sheet shown, the culture, the Linked Tables, each Sheet's Entries, Cell Formats and column widths, never a Value
- [x] An unknown version, or anything not understood, is refused by name
- [x] A Sheet Document of every readable version opens as a Workbook of one Sheet
- [x] `CONTEXT.md`'s Workbook Document entry holds

**Blocked by:** 01.

**Settled while building (2026-10-20):** the document tells itself from a Sheet Document by its
`workbookVersion`; each Sheet in it is recorded as the current Sheet Document records one, less the
version, the culture and the Linked Tables, which are the Workbook's. A refusal is a
`SheetDocumentException`, as a Sheet Document's is. `Workbook.Shown` is the Sheet shown.
