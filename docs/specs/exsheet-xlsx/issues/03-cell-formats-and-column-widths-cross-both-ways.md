# 03: Cell Formats and column widths cross both ways

Status: done

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §4 and §8.

- [x] Number Formats the engine reads are carried; one it does not read refuses the file by name
- [x] Horizontal alignment, font colour, bold, italic, underline, strikethrough, solid fills, ADR-0071's borders, column widths in Excel's unit
- [x] Theme colours resolved through the file's theme to RGB; the theme link listed as not carried
- [x] What §4 lists as not carried, listed in the Open Report
- [x] Layer 1 tests; Change Note

**Blocked by:** 02.

**Settled while building (2026-10-21):** the reading half lives in `XlsxWorkbookReader` and
`StyleReading`. The writing half is `StyleTable` (a format to a cell format index in the styles part)
and `SheetFormatPlan` (a Sheet's column, row and cell formats and widths as the file writes them).
Both are internal for ticket 04's writer, and the tests write through them (`Saved`). The file layer
gained a row's cell format (`XlsxRow`: `<row s customFormat="1">`, read, and written by
`XlsxSheetWriter.WriteRow`) and a column's `bestFit`.

- **Levels.** Excel's levels are not ADR-0071's merge: a cell's own format stands alone, a formatted
  row replaces its columns' format, and a column's format reaches only cells the file does not hold.
  Read, a row records the parts that differ from the default, plus any part a column under it
  records. A cell records only the parts that differ from what its row, column and the default show,
  so the document shows what Excel shows. Written, each cell is written with the format it shows.
  Where a formatted row crosses a formatted column with no cell of the document, and what the
  crossing shows differs from the row's, a cell of no value is written there. A Sheet formatted on
  many rows and columns can write many such cells. Ticket 04 judges whether that needs a bound.
- **Number Formats.** A known built-in id (0–4, 9–13, 37–40, 45–49 fixed; 14–22 and 6/8 in the
  engine's localised forms) is read as the engine's own built-in, and a code a file writes over it is
  ignored, as Excel does. An id outside those (5, 7, 23–36, 41–44, 50–163) is the locale's. It is read
  from the code the part writes, and refused when there is none, because its code follows the
  computer's locale. A file's own code is read as written (`NumberFormat.TryParseAsWritten`). One
  that spells a localised built-in (`m/d/yyyy`) is the file's own spelling, shown as written in every
  culture. A code the engine does not read refuses the file only where a cell, row or column uses it,
  naming the place (`B7`, `B:C`, `5:5`) and the code. `[h]:mm` and other elapsed formats are among
  them, which also settles ticket 02's 1904 elapsed durations: they are refused, not moved. Written,
  a localised built-in takes its id, and any other code is written from 164 in the engine's spelling.
- **Kept, as nearest and listed:**
  - An alignment ADR-0071 does not hold (fill, justify, centre across selection, distributed) opens
    General.
  - A double or accounting underline opens as a single one.
  - A solid fill of the automatic colour opens as no fill.

  Each is listed (`HorizontalAlignment`, `Underline`, `PatternFills`).
- **Font colour.** The workbook font's own colour (`fonts[0]`), where it resolves to black or names
  none, is Automatic. Excel's Text 1 is therefore the default and not listed. Any other colour is its
  RGB, a theme colour resolved with its tint. A theme colour anywhere the document uses lists
  `ThemeColours` once, after the workbook's own entries. A typeface or size other than the workbook
  font's is `FontNamesAndSizes`.
- **Widths.** The width a file stores is `trunc((c·7+5)/7·256)/256` for `c` characters, and back
  `trunc((w·7−5)/7·100+0.5)/100`: Calibri 11's 7 px digit and 5 px of padding, without Excel's pixel
  snapping. Every width to the hundredth crosses exactly. The default width (8.43) records nothing,
  and widths over 255 are clamped. `bestFit` is widened by entry; any other width is the user's. A
  Sheet's own default width (`defaultColWidth`, or a `baseColWidth` other than 8) is listed as
  `DefaultColumnWidth`, not carried.

Questions for ticket 07, which these readings rest on:

- how Excel shows ids 5–8 with and without a written code under other locales;
- widths when the workbook font is not Calibri 11 (a Mac's `baseColWidth="10"`), and whether Excel
  snaps a width written without pixel snapping;
- what Excel paints for a solid fill of the automatic colour.
