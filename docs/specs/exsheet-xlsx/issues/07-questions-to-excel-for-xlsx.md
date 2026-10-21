# 07: Questions to Excel for `.xlsx`

Status: ready-for-human
Roadmap: internal

**Why:** ADR-0330 builds nothing from reasoning about Excel. In the next Windows run:

- [ ] A file ExSheet saved opens in Excel with no repair prompt, recalculates, and shows the same Values
- [ ] The corpus files' readings (Number Formats, theme colours, widths) match what Excel shows
- [ ] A Linked Table's worksheet opens as an Excel Table whose Formulas answer
- [ ] Built-in Number Formats 5 to 8 (currency): how Excel shows them under other locales, with the code the file writes and
      without one (ticket 03 reads them from the code, and refuses one with none)
- [ ] Column widths when the workbook font is not Calibri 11 (a Mac's `baseColWidth="10"`), and whether Excel snaps a width
      ExSheet writes, unsnapped, to whole pixels (ticket 03's `ColumnWidthUnit`)
- [ ] What Excel paints for a solid fill of the automatic colour (ticket 03 opens it as no fill, and lists it)
- [ ] Whether `bestFit` marks a width a typed entry widened, or one fitted by a double-click (AutoFit): ticket 03 reads either
      as widened by entry
- [ ] A `<col>` that records a style at the default width without `customWidth` shows at the default width; and whether a
      `<col style>` with no width at all is a width of 0, as ticket 03 believes

**Blocked by:** 05.
