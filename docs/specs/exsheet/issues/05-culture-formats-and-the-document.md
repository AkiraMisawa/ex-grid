# 05: Culture, number formats, dates, and the versioned Sheet Document

Status: done

**What to build:** A Sheet has a declared culture that decides how typed constants are read and how Values show.
Constants are recorded parsed. Dates are serials in Excel's 1900 system, including its
29 February 1900. Excel's number format codes and horizontal alignment are settable per cell.
Values show at most 15 significant digits, and `####` when they do not fit. The Sheet Document
carries a version and the culture, and a reader refuses a version it does not know.

**Blocked by:** 02

- [x] `1,234.5` under `en-US` and `2026/9/26` under `ja-JP` become a number and a date (ADR-0048)
- [x] A document saved under `en-US` and opened under `de-DE` shows the same numbers (ADR-0048)
- [x] Date serials match Excel's around 1900-02-29 (ADR-0047)
- [x] Format codes for numbers, percent, thousands and dates render as Excel's under the culture
- [x] A number too wide for its column is `####` (ADR-0016)
- [x] An unknown document version is refused, not guessed at (ADR-0048)

## Comments

2026-09-27, engine half: a `Sheet` has a declared culture; `Sheet.Enter` reads typed numbers
(the culture's group and decimal separators, parentheses, exponent, `%`, fifteen significant
digits kept), dates in the culture's day order (and year-first ISO), times, booleans and Error
Values, and records them parsed; a date or percentage typed into a General cell gives it the
implied format, as Excel does. Serials follow Excel's 1900 system, 29 February 1900 included
(serial 60), and serial 0 shows as 0 January 1900. `NumberFormat` reads a declared subset of
Excel's codes (sections, `0 # ?`, thousands and scaling commas, `%`, `E+00`, `@`, literals, and
`y m d h s AM/PM`) and refuses the rest by name; `Sheet.GetDisplay` formats under the culture
with at most fifteen significant digits and resolves General alignment. Alignment is settable
per cell. The Sheet Document records version 1, the culture, Entries, formats and alignment,
and refuses an unknown version, a missing one, and anything version 1 does not define
(`CultureTests`, `NumberFormatTests`, `SheetDocumentTests`). **What remains is the component's:**
`####` for a number too wide for its column (ADR-0016) — the engine reports
`CellDisplay.IsNumber`, and `CellDisplay.CannotShow` for a date no width can show — and the
commands that set formats and alignment.

2026-09-27, component half: each cell's value in ExGrid is a small immutable object carrying the
engine's formatted text and whether it is a number (`SheetCellText`); the column's `Format`
unwraps the text, and `CellType` answers Number or Text per cell from the same object. The
`CellType` delegate reads only the row it is handed, so it is held once and never replaced: a cell
whose kind changes arrives on a new row instance (ADR-0003). `####` is ExGrid's own rule for a
Number cell that does not fit (DC-26/SH-10), with the number as the accessible name; a number no
format can show (`CellDisplay.CannotShow`) is handed to the grid as a run of `#` no column can
hold, which the same rule turns into the `####` that fills the cell. `ExSheet.SetNumberFormatAsync`
and `ExSheet.SetAlignmentAsync` set either on the Selection as one undoable step (the Context Menu
will call them), refusing by name a selection of more than 100,000 cells. Layer 2:
`SheetDisplayTests`.

Known gaps, both reported to the orchestrator rather than worked around:

- **Alignment is recorded but not painted per cell.** ExGrid's per-cell declaration is the kind,
  which gives Number cells the right and Text cells the left; a cell's explicit alignment, and
  Excel's centring of booleans and Error Values under General, have no per-cell route in the core
  (`GridColumn.Align` is per column). Painting them needs a core declaration.
- **General does not shorten to the column's width.** Excel's General format shows `=1/3` as
  `0.333333` in a default column; the engine gives fifteen significant digits and the grid, rightly
  by ADR-0016, hashes what does not fit. The default column holds 8.43 of the grid's digits, so a
  ten-character date such as `2026/09/26` is `####` until the column is wider.

2026-09-27, engine, General fits the column (SH-20, ADR-0047 second round): the gap above is
closed on the engine's side. `Sheet.GetDisplay(address, width)` takes the column's width in
Excel's unit — characters, `Sheet.DefaultColumnWidth` = 8.43 — and fits a General number to it
as Excel does: decimals rounded (`=1/3` reads `0.333333` at 8.43), scientific where the integer
part does not fit or has twelve or more digits (`123456789` reads `1.23E+08`), never more than
eleven characters besides the sign, and `CannotShow` where not even `1E+08` fits. A number in
any other format, and a date, longer than the width `CannotShow` (`####`). Each character is
charged one digit width; the component converts its pixel width as
`(columnPx − 2 × paddingPx) / digitWidthPx`, the inverse of how it sizes its default column.
`GetDisplay(address)` keeps the fifteen-digit text for the accessible name and the copy.
`Sheet.GetWidthOnEntry(address)` answers the width a typed number or date needs, for widening a
default-width column on entry; the component wires both. The cases are
`ExcelCases/general-width.json` (five documented, the rest uncertain until the oracle asks
Excel); the oracle now sets a case's `columnWidth` and records the width Excel leaves a column
at. FMT-033/036/038 now expect what a wide column shows (`0.333333333`, `1.23457E+14`,
`0.666666667`) instead of fifteen digits, which Excel never shows in a cell; twelve or more digits
going scientific is Microsoft's own documentation, the eleven characters are uncertain.

2026-09-27, engine, format levels (SH-21, ADR-0047 second round): number formats and alignment
live at cell, row and column level, cell over row over column. `SheetEdit.SetFormat(range, …)`
and `SheetEdit.SetAlignment(range, …)` (and the `Sheet` methods of the same names) record whole
columns (`CellRange.Parse("B:D")`, `CellRange.WholeColumns`) and whole rows (`"2:4"`,
`CellRange.WholeRows`) as one entry each, and the whole Sheet (`"A:XFD"`) as one run; a cell in
them that set its own format takes the new one, as Excel's do, and one step undoes it all exactly.
A cell records a format only where it differs from what its row or column gives it. The
per-address `SetFormat`/`SetAlignment` the component calls are unchanged, so **the component can
drop its 100,000-cell cap for a selection of whole columns or rows by passing the range instead
of its cells** (its to wire). Insertion gives new rows the row above's row format, new columns
the column to the left's, and formats move with their rows and columns; deletion drops them;
undo restores every level. Copy and fill carry the format a cell shows, whichever level set it.
The Sheet Document is version 3 (`columns` and `rows` as runs, a cell's explicit `General`
recorded when a level would otherwise show through) and still reads versions 1 and 2;
`SheetDocumentCell.Format`/`Alignment` are now nullable, null meaning "takes its row's or
column's". Cases: `ExcelCases/format-levels.json` (insertion's Format Same As Above/Left
documented, the precedence cases uncertain); C#: `FormatLevelTests`, `SheetDocumentTests`.
