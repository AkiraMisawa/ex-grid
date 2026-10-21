# 03: Cell Formats and column widths cross both ways

Status: ready-for-agent
Roadmap: ExSheet — Number Formats, alignment, font colour and style, fills, borders and column widths open and save with an `.xlsx`.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §4 and §8.

- [ ] Number Formats the engine reads are carried; one it does not read refuses the file by name
- [ ] Horizontal alignment, font colour, bold, italic, underline, strikethrough, solid fills, ADR-0071's borders, column widths in Excel's unit
- [ ] Theme colours resolved through the file's theme to RGB; the theme link listed as not carried
- [ ] What §4 lists as not carried, listed in the Open Report
- [ ] Layer 1 tests; Change Note

**Blocked by:** 02.
