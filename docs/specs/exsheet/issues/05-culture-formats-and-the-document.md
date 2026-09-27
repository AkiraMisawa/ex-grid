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

2026-09-27, engine, a Formula's format at entry (ADR-0047 second round): `Sheet.Enter` (typing
and pasted text) gives a Formula entered into a General cell the format of the first formatted
cell it reads, for Formulas of simple arithmetic only — single-cell References, constants, `+`,
`-`, parentheses — so a date plus a number, and a date minus a date, show as dates. Once, at
entry; a cell already formatted keeps its format; the entry's step undoes it. Microsoft documents
this only for `DATE`, `TODAY` and `NOW`, which are not declared, so all fifteen cases in
`ExcelCases/formula-formats.json` are uncertain; multiplication, functions and `&` give no format
(the engine's answer, recorded for the oracle to confirm or correct).

2026-09-27, component, alignment painted per cell (DC-29, ADR-0050 item 7): the first known gap
above is closed. `SheetCellText` carries the cell's `CellAlign`, read from the engine with the
rest of the cell: a user's Left, Center or Right as set (`Sheet.GetAlignment`, cell over row
over column); General as `Auto`, so the per-cell kind aligns numbers right and text left; and
General's centre where the engine's `CellDisplay` resolves it there, which is Excel's place for
booleans and Error Values. ExSheet hands it to ExGrid's `CellAlign` as one delegate held for the
process (`SheetColumns.CellAlign`), reading only the row it is handed, so an alignment change
arrives on the new row instances the engine's change names and no other row renders (ADR-0003).
Layer 2: `SheetDisplayTests` (General, booleans and Error Values, a user's alignment and its
undo, the held delegate and the repainted rows).

2026-09-27, component, column widths and widening on entry (SH-20, ADR-0047 second round), in
part: ExSheet hands the grid its own `SheetColumnList` over the shared `SheetColumns.All`, a new
instance only when a width is set, so an ordinary edit never looks to a row like a change of
columns (ADR-0003). It declares `OnColumnWidthChanged`: a resize is recorded as Fixed and makes
the column the user's. A number or date the user types into a column still at its default width
widens it when `Sheet.GetWidthOnEntry` exceeds it, to the pixels the grid's own estimate charges
for the text the cell shows at that many characters, in the Cell Metrics the grid resolves
(`GridMetrics.Resolve` over the cascaded `GridPresentationDefaults`), so the grid does not hash
it; a widened column is still at its default width and widens again, and never narrows; a column
the user resized never widens; only a typed entry widens (not a paste, a fill or a
recalculation); replacing the document resets the widths. Declaring the width change brings
ExGrid's column menu button with it (the grips render only with it), whose one enabled command is
Size to fit; the sort commands in it are disabled (ADR-0046). Layer 2: `ColumnWidthTests`,
`SheetRenderingTests.Sort_and_filter_are_not_wired`.

**Blocked: General fitted to the column is not painted.** ADR-0047's third round requires the
painted text to be fitted (`GetDisplay(address, width)`) while the accessible name keeps the
unfitted text. ExGrid gives a cell an accessible name of its own only when it hashes it
(`aria-label` on a `####` cell, ExGridRow); on every other cell the painted text is the name.
Painting `0.333333` would therefore make the screen reader hear `0.333333`, which the ADR rules
out, and ExGrid's own copy (`text/plain` from the column's `Format`) would carry the fitted text
until the copy answer (ADR-0050 item 9) is wired. The proposal returned to the orchestrator is a
core declaration in ADR-0050's pattern: a per-cell painted text that the grid asks for with the
column's resolved content width, painting the answer and giving the cell the value's own text as
its accessible name wherever the two differ. The grid holds the resolved width and metrics, so
the Consumer would neither replicate the metrics resolution nor track widths for this.

Known gaps: widths are the component's, not the Sheet Document's, so they are not saved, and an
inserted or deleted column does not move them (formats move; widths stay at their index); the
default width is sized from ExGrid's default Cell Metrics, not a Wrapper's. Whether a column
widened by an entry narrows again when that entry is undone is Excel's to observe; here it does
not.

2026-09-27, component, formats on levels (SH-21, ADR-0047 second round): the interim cap is gone
(`ExSheet.FormatCellCap` and its refusal are removed). `SetNumberFormatAsync` and
`SetAlignmentAsync` hand a one-range Selection to the engine as a `CellRange`
(`SheetEdit.SetFormat(CellRange, …)` / `SetAlignment(CellRange, …)`), so a Selection of whole
columns or whole rows — a header click, `B:C` in the Name Box — records one entry per run of
columns or rows, and a cell typed there later takes the level's format. A Selection of several
ranges is one step over their cells, uncapped (principle 5), except where one of the ranges is
whole columns or rows: the engine takes one range per step, and cell by cell that would be a
million cells per column, so it is refused by name and nothing changes. Layer 2:
`SheetDisplayTests` (whole columns recorded as columns and undone as one step, a later entry in a
formatted column, whole rows aligned, several ranges as one step, several ranges with whole
columns refused).

2026-09-27, component, General fitted to the column is painted (SH-20, DC-35; ADR-0047 third
round, ADR-0050 item 11): the block above is closed. ExSheet hands ExGrid a `PaintedText`
(`SheetColumns.PaintedText`), one delegate for the process that reads only the row it is handed.
The grid gives it the column's content width in pixels and its Cell Metrics; ExSheet converts
that to Excel's unit as `contentWidthPx / DigitWidthPx` (every character charged one digit
width, the inverse of how the default column is sized) and answers, for a number in General,
`Sheet.GetDisplay(address, width)`'s text — the run of `#` that becomes `####` where the engine
says it cannot be shown — and null wherever that equals the value's own text, and for text,
booleans, Error Values and other formats, which the grid's `####` rule still decides. A row
keeps each answer per width, and a row the engine's change names arrives as a new instance, so
an edit repaints only its row. The accessible name is the value's own fifteen-digit text wherever
the paint differs (the core does this), and a copy is the engine's unfitted Value. So `=1/3`
reads `0.333333` in a default column and `0.333333333` in one eleven digits wide, and
`123456789` reads `1.23E+08`. Layer 2: `PaintedTextTests`; `SheetDisplayTests` now pins `####`
on a formatted number, since General is fitted. **Open:** the engine charges `E`, `+`, `-` and
`.` one digit width each, while the grid's `####` decision measures their own widths; a
character wider than a digit could still hash a fitted text. That is ADR-0047's stated estimate
until the case corpus has observed Excel.
