# ExSheet opens and saves `.xlsx`

Status: ready-for-agent

Decided in [ADR-0330](../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md)
(Q1 to Q5 and X1 to X4, 2026-10-20). This spec gathers it into work; it decides nothing the ADR does
not. It builds on the Workbook ([ADR-0331](../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md),
its own spec under `exsheet-workbook`).

## Problem Statement

A user's spreadsheets live in `.xlsx`. An ExSheet application today can take only its own Sheet or
Workbook Document, so a user cannot bring a workbook from Excel, and cannot hand what they built back to
someone who works in Excel.

## Solution

Two packages with no dependency outside the BCL. **`ExGrid.Data.Xlsx`** reads and writes the file
itself: the zip, its parts, shared strings, styles, and cells as a stream. **`ExSheet.Xlsx`** maps a
file to a Workbook Document and a Workbook to a file. Opening a file returns, beside the document, an
**Open Report** of what was not carried. Anything that would change a Value or the number a cell shows
is refused by name, and nothing opens. ExSheet takes an opening at its own place in the Input Order.

## User Stories

1. As a user, I want to open an `.xlsx` and see every worksheet as a Sheet, in the file's order, with the worksheet that was shown shown.
2. As a user, I want numbers, text, booleans, errors and Formulas carried, and every Value computed again, so that what I see is ExSheet's answer and not a cached one.
3. As a user, I want a file ExSheet cannot read faithfully refused, with the Sheet, the cell and what it held, so that I never work on a quietly wrong copy.
4. As a user, I want what was left behind (fonts, merged cells, comments, …) listed when the file opens, so that I know what a save will not keep.
5. As a user, I want a Formula naming a function ExSheet does not support to open as `#NAME?` and be listed, so that I know before I meet it.
6. As a user, I want Number Formats, alignment, font colour and style, fills, borders and column widths carried both ways.
7. As a user, I want theme colours to look as they did in Excel.
8. As a user, I want to save the Workbook as an `.xlsx` that Excel opens and recalculates, and that a program which does not calculate still reads with the Values I saw.
9. As a user, I want a Linked Table a Formula reads to be saved as an Excel Table on a worksheet of its own, so that the Formula keeps working in Excel.
10. As a user, I want a save refused by name while I am editing a cell, or while a Linked Table has not answered.
11. As a user, I want a large file to open without freezing the page, to see that it is opening, and to cancel with Escape.
12. As a user, I want a key typed while a file opens to land on the opened Workbook, never on the one before, whatever the file's size.
13. As an application developer, I want to declare the Workbook's culture when I open a file, so that typing `1,234` means what my users mean.
14. As an application developer, I want limits on a file's size and cell count that refuse a hostile file by name, and that I can raise.
15. As an application developer, I want to read and write files on a server with no Blazor in the process.
16. As an application developer, I want the file layer shared, so that a later Snapshot import reads the same zip the same way.
17. As a Docs Site reader, I want a page and an Example that opens a file, shows its Open Report and saves it, with the Built-in / MudBlazor switch.

## Implementation Decisions

- **`ExGrid.Data.Xlsx` references `ExGrid.Data` only**; `ExSheet.Xlsx` references it and `ExSheet.Engine`.
  Neither references a component. Both pack and are smoke-tested like the others (ADR-0042).
- **Worksheets are read as a stream** with `XmlReader`, DTDs prohibited; shared strings are the one part
  held whole. Reading works in batches, reporting progress and yielding between them.
- **Limits** on the uncompressed size and the cell count refuse a file by name; the defaults come from
  a measurement recorded in ticket 01.
- **Formulas**: `_xlfn.` and `_xlws.` are taken off on reading and put back on writing; shared formulas
  are expanded into each cell's own; a spilled array is its anchor's Formula.
- **Writing is taken from the Workbook as it stands**, each Formula with its Value, the workbook marked
  for full calculation on load.
- **ExSheet gains a method that replaces its Workbook with one still being read**, taken at its place in
  the Input Order, cancelled by Escape, said aloud while under way (ADR-0330 §10). Saving is taken at its
  own place and refused while an edit is open.
- `CONTEXT.md` gains **Open Report** with the first ticket that returns one.

## Testing Decisions

- **Seams**: `ExGrid.Data.Xlsx`'s reader and writer (layer 1) over files built in the test and a small
  corpus of files saved by Excel; `ExSheet.Xlsx` (layer 1) from a file to a Workbook Document and back;
  the `ExSheet` component (layer 2) for the opening in the Input Order; layer 3 for the Docs Example and
  Escape at machine speed on both hosts.
- A round trip (open, save, open) is asserted for everything §4 of the ADR carries.
- Expected readings of files Excel saved are checked against Excel in a Windows run (ticket 07), not
  assumed.

## Out of Scope

Excel Tables read back (an ADR of their own); a worksheet read into a Snapshot for ExGrid or ExPivot
(an ADR of its own); `.xlsm`, `.xlsb`, `.xls`; everything ADR-0330 §4 lists as not carried.

## Further Notes

The tickets are under `issues/`, worked in number order where their blocking edges allow.
