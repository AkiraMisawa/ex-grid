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
`StyleReading`. The writing half is `StyleTable` (a format to a cell format index in the styles part,
for the Workbook's culture) and `SheetFormatPlan` (a Sheet's column, row and cell formats and widths
as the file writes them). Both are internal for ticket 04's writer, and the tests write through them
(`Saved`). The file layer gained a row's cell format (`XlsxRow`: `<row s customFormat="1">`, read,
and written by `XlsxSheetWriter.WriteRow`; `XlsxWorksheet.Rows`, empty for none) and a column's
`bestFit` and `customWidth`.

- **Levels.** Excel's levels are not ADR-0071's merge: a cell's own format stands alone, a formatted
  row replaces its columns' format, and a column's format reaches only cells the file does not hold.
  Read, a row records the parts that differ from the default, plus any part a column under it
  records. A cell records only the parts that differ from what its row, column and the default show,
  so the document shows what Excel shows. Written, each cell is written with the format it shows.
  Where a formatted row crosses a formatted column with no cell of the document, and what the
  crossing shows differs from the row's, a cell of no value is written there. A Sheet formatted on
  many rows and columns can write many such cells; ticket 04 carries the bound.
- **Number Formats.** A known built-in id (0–4, 9–13, 37–40, 45–49 fixed; 14–22 in the engine's
  localised forms) is read as the engine's own built-in, and a code a file writes over it is
  ignored, as Excel does. Every other id below 164 (the currencies 5–8, 23–36, 41–44, 50–163)
  follows the computer's locale. It is read from the code the part writes, and refused, naming the
  id, when there is none. A currency is never shown in the reader's currency: `numFmtId="8"` spelled
  in euros opens in euros under en-US. A file's own code is read as written
  (`NumberFormat.TryParseAsWritten`). One that spells a localised built-in (`m/d/yyyy`) is the file's
  own spelling, shown as written in every culture. A code the engine does not read refuses the file
  only where a cell, row or column uses it, naming the place (`B7`, `B:C`, `5:5`) and the code.
  `[h]:mm` and other elapsed formats are among them, which also settles ticket 02's 1904 elapsed
  durations: they are refused, not moved. Written, a localised date or time built-in takes its id.
  The engine's built-in currency is written as a code of the file's own, spelled in the Workbook
  culture's currency (`NumberFormat.LocalCode`), so it means the same amount under every locale
  Excel opens it in, never as a bare 6 or 8. Any other code is written from 164 in the engine's
  spelling.
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
- **Widths.**
  - The width a file stores is `trunc((c·7+5)/7·256)/256` for `c` characters, and back
    `trunc((w·7−5)/7·100+0.5)/100`. This uses Calibri 11's 7 px digit and 5 px of padding, without
    Excel's pixel snapping, and every width to the hundredth crosses exactly.
  - Read:
    - The default width (8.43) records nothing.
    - A width over 255 is clamped.
    - A width above 0 but too narrow for a hundredth of a character opens at 0.01, the narrowest
      the engine holds.
    - A width of 0 is a column Excel shows hidden, listed as `HiddenColumns`.
  - `bestFit` is read as widened by entry. Excel writes it for a double-click AutoFit too, so the
    file does not tell the two apart. Any other width is the user's.
  - A Sheet's own default width (`defaultColWidth`, or a `baseColWidth` other than 8) is listed as
    `DefaultColumnWidth`, not carried.
  - Written, every `<col>` carries a width. A column that records only a format is written at the
    default width without `customWidth`, because a `<col>` with no width is believed to be read as
    0.

The questions to Excel these readings rest on are in ticket 07.
