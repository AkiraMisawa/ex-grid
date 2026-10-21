# 02: A Workbook Document is read from a file, with its Open Report

Status: done

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §3, §4, §5 and §7.

- [x] A new shipped package `ExSheet.Xlsx` (refs `ExGrid.Data.Xlsx`, `ExSheet.Engine`), packed and smoke-tested
- [x] Every worksheet a Sheet, in order, the shown one shown; constants and Formulas carried, `_xlfn.`/`_xlws.` taken off, shared formulas expanded, spilled arrays as their anchor's Formula; 1904 serials moved to 1900's
- [x] The culture taken from the caller
- [x] Refused by name with Sheet and cell: an unreadable Formula, a defined name, another workbook, an Excel Table reference, a legacy array formula
- [x] The Open Report: what was not carried, and cells naming unsupported functions
- [ ] `CONTEXT.md` gains Open Report; layer 1 tests; Change Note *(left for the orchestrator: the layer 1 tests and the Change Note are done, and `CONTEXT.md` already holds **Open Report** on `main`; a background agent does not change the glossary)*

**Blocked by:** 01.

**Settled while building (2026-10-21):** the package is `src/ExSheet.Xlsx`, net10.0, referencing
`ExGrid.Data.Xlsx` and `ExSheet.Engine` at exactly its own version and no component; it is packed and
checked by `tests/ExGrid.PackageSmoke/check.sh` into `.feed-unreleased`, held back from the release
with `ExGrid.Data.Xlsx` until ADR-0042 names them, and the trimmed round trip opens the workbook it
writes through it. `XlsxWorkbookReader.ReadAsync(stream, culture, options, token)` returns an
`XlsxOpenResult`: the `WorkbookDocument` and its `OpenReport` (`NotCarried`, each a `NotCarriedKind`
with its Sheet, a detail and a sentence; and `UnsupportedFunctions`, each cell with the functions it
names). A refusal is an `XlsxOpenException` with an `XlsxOpenRefusal` reason, the Sheet, the cell and
what it `Held`; the file layer's `XlsxException` is wrapped as `XlsxOpenRefusal.File`, its reason kept
in `FileRefusal`.

- **Formulas.** `_xlfn.`, `_xlws.` and `_xlpm.` are taken off outside quotes (`_xlpm.` is how a file
  stores a `LET`'s names; ticket 04 puts it back), and `_xlfn.ANCHORARRAY(A1)` is read as `A1#`. A
  shared formula's cells are its master's Formula moved as a copy moves it (`Entry.MovedBy`). A
  dynamic array is its anchor's Formula, and the values the file stored over its spill are not
  carried. Before the engine reads a Formula, a `[` after a name is an Excel Table, a `[n]` or a
  quoted Sheet name starting `[` another workbook, and `_xlfn.SINGLE` (Excel's `@`) unreadable.
- **Names.** A name a Formula reads that nothing in it binds (`Entry.UnboundNames`) is refused when
  the workbook defines it, and, where the workbook holds an Excel Table (a part under `xl/tables/`),
  when it does not: a bare name may be a Table's. Otherwise it is `#NAME?` in Excel too, and carried.
  A defined name called as a function (a `LAMBDA` in Name Manager) is refused as a defined name.
- **Every legacy array formula is refused, one cell's included**, as this ticket's line says: Excel
  shows a one-cell legacy array's first value, and ExSheet would spill it.
- **1904.** A constant number whose Number Format shows a date (a built-in date format, or a code
  with a year, a day, or a month in a section without hours or seconds) moves by 1,462 days. A time
  of day, a duration (`[h]:mm`) and any other number are the same number on both systems, and stay.
  A Formula is carried as written, and computes on 1900's.
- **Not carried, as far as the file layer reports it:** hidden sheets (opened shown), chart sheets,
  defined names, row heights, hidden rows and columns, frozen panes, and each worksheet element the
  file layer lists (merged cells, conditional formatting, data validation, notes, hyperlinks,
  drawings, protection, print settings, AutoFilter, Excel Tables, sheet properties, and any other
  element by name), rich text, and the workbook's PivotTables, external links, data connections and
  slicers, from its parts. Cell formats, column widths and the theme link are ticket 03's, and are
  not listed yet.
- **The engine gains, for readers like this one** (ADR-0201's sibling rule): public constructors
  `SheetDocument(culture, name, cells)` and `WorkbookDocument(culture, shown, sheets)`, validated as
  `FromJson` validates; `Entry.MovedBy(rows, columns)`; `Entry.UndeclaredFunctions` and
  `Entry.UnboundNames`.
- The file layer's limits pass through unchanged. Ticket 01 expected that ExSheet's opening might want
  a lower `MaxCells` of its own; nothing was measured here, so none is set.
- No Docs Site page yet: the page and its Example come with ticket 06.

