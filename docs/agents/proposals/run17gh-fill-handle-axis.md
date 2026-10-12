# Run 17 G-H, the fill handle on a whole axis: what the orchestrator writes

Branch `claude/run17gh-fill-handle-axis`, from `main` at `a533de01`. Round 3, after the readings of
2026-10-12 (`verification/2026-10-12-windows-readings/report.md` on
`claude/exsheet-windows-readings-2026-10-12`, R5; its pictures `shots/r5-*`).

**The user's decision**, taken on 2026-10-12 on the seventeenth Windows run's h3
(`verification/2026-10-11-windows-17/report.md` on `claude/exsheet-windows-verify-17`, Part H;
`part-h/shots/h-h3-1-corner-zoom.png`, `h-h3-2-corner-zoom.png`):

> The fill handle on a whole axis follows Excel: a range spanning every row puts its handle at its
> first row's top-right corner, one spanning every column at its first column's bottom-left. Ctrl+A,
> several whole columns or rows, and which way the drag fills are asked next run, and keep today's
> behaviour until then.

**What R5 observed**, which this round builds (each text below says "observed (R5, case)", "by
generalisation" or "a reading"):

1. Ctrl+A (`1:1048576`, A1 active): no handle in view; the only square of #217346 found was the
   select-all corner's triangle.
2. Columns B:D from the Headings (B1 active): the handle at the selection's top right, straddling the
   Heading's bottom edge over D1's right edge. Rows 2:4 (A2 active): at the bottom left, on the row
   Heading's edge under row 4.
3. Column C (C1 `1`, C3 `a`, C20 `=ROW()`, C1048576 `z`) dragged two columns right: the selection
   became C:E, whole columns; D and E filled down the whole column; D1 `2`, E1 `3` (a series), D3
   and E3 `a`, D20 and E20 `=ROW()`, D1048576 and E1048576 `z`; D2 empty. One cell's handle (C5 `1`
   dragged two rows down) copied: 1, 1, 1. Dragged down, nothing changed.
4. Row 3 (A3 `1`, C3 `a`, H3 `=COLUMN()`, XFD3 `z`) dragged two rows down: rows 4 and 5 filled
   across, A4 `2`, A5 `3`, `a` copied, `=COLUMN()` filled. Dragged sideways, nothing changed.
5. Column C with row 1 scrolled out of view (three presses on the scroll bar's arrow): the handle
   still drawn at the top right of the part in view, clipped to 6 × 4 px under the Heading.

## What was done

| Where | Before this round | Now | Excel |
|---|---|---|---|
| ExGrid, `FillRules.HandleCorner` (internal) | every cell (Ctrl+A) kept the bottom-right corner | returns none for a range spanning every row and every column; ExGrid's `FillHandleRange` is then null, so nothing is painted or grabbed | none in view: observed (R5, 1). None anywhere, at a small grid's bottom-right too: by generalisation (there is nowhere left to fill to) |
| ExGrid, several whole columns or rows | the last column's top-right, the last row's bottom-left (round 2, called "the rule for one, unchanged") | unchanged | observed (R5, 2) for B:D and 2:4 selected from their first Heading. Which end the handle takes when the Extent is the first column or row: not read |
| ExGrid, `SelectionStyles.Handle`, `HandleCentrePx` | a whole column's handle went with its first row: none once row 0 left the painted rows | held at the readable area's top while the first row is above it: its top is `max(first row's top, readable top)`, from `_scrollTopPx`; a whole row's left is `max(first column's left, scrollLeft + PinnedWidthPx)` in the scrollable layer | a column: observed (R5, 5). A row: by generalisation |
| ExGrid, `IsOnFillHandle`, `PressTakenAt`, `ex-grid.js` | the press carried the horizontal scroll only | `takenAt` also reads `scroller.scrollTop`, and `PressTakenAt` takes it after `scrollLeftPx`; the hit test places a held handle by the scroll the press was taken at (ED-31) | — |
| ExGrid, the drag | one axis, from wherever the handle stands | unchanged: a whole column fills across, a whole row up or down; along the axis it spans nothing is reached; the Selection after is source and target, whole columns or rows | observed (R5, 3 and 4) |
| ExSheet.Engine, `Sheet.FillRule` | a single number in a line was copied | in a fill from whole columns or whole rows (`CellRange.IsWholeColumns` across, `IsWholeRows` down), a single number in a line goes on by one per cell, held at 15 significant digits as the trend is; one cell, and any other source, still copies | a whole column and a whole row going on by one, one cell copying: observed (R5, 3, 4 and its control). Counting back by one filled left or up, and the 15 digits: by generalisation. A lone number in a source of several cells that is not a whole axis: not read, still copied |
| ExSheet.Engine, `Sheet.PlanFill`, `LevelsOf`, `DoFillWithLevels` | round 2: every line of a whole axis filled when a format lay along the fill (C:C bold into D:E wrote a Bold into some two million cells) | a fill from whole columns (whole rows) writes each target column's (row's) format as the level its source column (row) records, then settles and writes only the lines holding a cell; one step undoes both | not read: R5's column held no format. Recording it on the column is ADR-0047's whole-column rule applied to a fill (by generalisation) |

**Why the levels change no decided rule.** What each target cell shows, and its own four sides
(ADR-0071's copy rule: "writes each target cell's own four sides, and touches no neighbour"), are
what writing the cell would give it: a cell's own part is its row's, else its column's, so a line
holding no cell carries its row's format over the source column's, and shows the same over the
target column once that records the source column's. Rows and columns with formats across the fill
need no cell writes either. The engine tests below pin it, and fail on round 2's code.

**Measured as round 2 measured** (`Sheet.Check` then `Sheet.Do` on one Sheet, a Stopwatch round
each, `GC.GetTotalMemory(true)` before and after; Debug build, .NET 10 on this Linux container; one
process per case; the scratch test is not committed):

| Case | Round 2's code (`3d1f3a11`) | This round |
|---|---|---|
| C1 `1`, C2 `2`, C3 `=C1+C2`; C:C into D:D, no format | Check 14 ms, Do 17 ms; heap 1 → 1 MB | Check 16 ms, Do 23 ms; 1 → 1 MB |
| R5's column C, C:C bold, into D:E | Check 2,472 ms, Do 14,058 ms, Undo 1,631 ms, Redo 3,702 ms; heap 1 → 931 MB; Sheet Document 79,567,034 characters | Check 37 ms, Do 17 ms, Undo 1 ms, Redo 0 ms; 1 → 1 MB; 424 characters |
| A2 `1`; row 2 into rows 3:4, no format | Check 17 ms, Do 22 ms; 1 MB | Check 13 ms, Do 36 ms; 1 MB |
| The whole Sheet bold, A3 `1`; row 3 into rows 4:100 | Check 597 ms, Do 3,637 ms; heap 3 → 391 MB | Check 9 ms, Do 75 ms; 3 → 6 MB |

**Choices of this branch, not the user's:**

- Every cell shows no handle anywhere, not only none in view.
- ExGrid's handle on a whole axis lies just inside the cells, beside the Heading (round 2's choice,
  kept). Excel's straddles the Heading's bottom edge (observed, R5 (2)) and with row 1 scrolled away
  the Heading covers its top (observed, R5 (5): 6 × 4 px in view). ExGrid's header and Row Headings
  stand above the selection layer (ADR-0008, 2026-10-01), so a straddling handle would be half
  covered; it lies half its side lower, and is wholly in view in both cases. Round 2's text called
  this "as Excel's does"; R5 shows it is not, and every text now says so.
- A held handle stands exactly at the readable area's edge, also mid-row (ExGrid scrolls by the
  pixel, Excel by the row), placed at render from the scroll the grid last read.
- A whole row's handle is held at the left of the part in view (by generalisation from the column).
- The lone-number series is limited to whole-axis fills, the case R5 observed; it is held at 15
  significant digits, and counts back by one going left or up.
- The level write applies to Ctrl+D and Ctrl+R over whole columns or rows too (`SheetEdit.FillCopy`
  plans through the same `PlanFill`); those copy, as the keys always do.
- Layer 3 drags column E one column right on `/sheet` (F lies clear of the right edge band, where
  round 2 found G unreliable); R5's two columns are layer 2's, in ExSheet's `FillWiringTests` and
  the engine's `FillTests`.

### Tests, each named with its ADR

- **Engine**, `tests/ExSheet.Engine.Tests/FillTests.cs`: round 2's two whole-axis tests rewritten to
  R5's cases (column C into D:E: D1 2, E1 3, `a`, `=ROW()` 20, `z`, D2 empty, D9's text cleared, undo
  byte for byte; row 3 into rows 4:5: A4 2, A5 3, `a`, `=COLUMN()` 8, `z`); one cell's lone number
  copied (R5's control); a whole column filled left counts back; Ctrl+R over whole columns copies;
  C:C bold with D yellow into D:E records one bold run C:E, no Fill, and no cell-level Font, and
  undo and redo give the documents back; a whole row's italic recorded on rows 4:5 with three cells
  written; a row format, a cell's own Number Format and a target cell's own Font under the new level
  show as writing each cell would. 32 in the class.
- **Layer 2, ExGrid**, `tests/ExGrid.Components/FillHandleTests.cs`: round 2's "goes with its first
  row" replaced by "held at the readable top" (`top: 0px` at 400, `top: 10px` at 410); a held handle
  grabbed where it stands and dragged across; a press told it was taken at 400 is a cell press at
  (99, 13), and told 410 the same offsets are the handle (fails if the hit test reads the grid's own
  last scroll); a whole row's handle at `left: 160px` with Book under the Row Headings; every cell
  shows none, and a press at its old corner selects. `PressTakenAtTests`, `SourceReplacedTests`,
  `WritesLandAsEnteredTests` pass the new argument; `ShippedStylesheetTests` pins
  `scroller.scrollTop` in `takenAt`. 28 in `FillHandleTests`.
- **Layer 2, ExSheet**, `tests/ExSheet.Components/FillWiringTests.cs`, six new: column C (R5's cells)
  from the Name Box, its handle at C1's top-right, dragged to E3: D1 2, E1 3, `a`, E20 `=ROW()`,
  D1048576 and E1048576 `z`, the Selection C:E, one undo step; dragged down, nothing and no undo
  step; row 3 dragged to A5: A4 2, A5 3, C5 `a`, H4 and H5 `=COLUMN()`, XFD5 `z`, the Selection
  3:5; Ctrl+A: every cell selected and no handle; C:C scrolled three rows, the handle at the top of
  the part in view and dragged from there; C:C bold filled into D paints D5 `ex-font-xb` and records
  one run C:D with no cell Font in D.
- **Layer 3**: `declarations.spec.mjs`, round 2's two cases rewritten to R5's on free cells of
  `/sheet` (E1 `1`, E3 `a`, E9 `=ROW()`: dragged down inside E, nothing; dragged to F3, F1 `2`, F3
  `a`, F9 `9` and `=ROW()` in the bar, G1 empty, one Ctrl+Z; A6 `1`, C6 `a`, E6 `=COLUMN()`: dragged
  across inside row 6, nothing; dragged to D8, A7 `2`, A8 `3`, C8 `a`, E8 `5`, one Ctrl+Z), and a
  new case: E:E scrolled three rows, its handle at E4's top-right, dragged to F6, F5 `x` and F1 `2`.
  `selection-look.spec.mjs`: under both Chromes, column C scrolled three rows, the handle centred on
  C4's right edge, its top on C4's top, below the Heading, its middle pixel the handle's colour; and
  Ctrl+A from F12 draws a range and no handle.

## The edits, in order

Each **Old** occurs once in its file at this branch's tip, and the list applies in order.

### 1. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
- **The core paints the handle** at the bottom-right corner of the Selection's last range, in the
  selection overlay, when the Consumer declares that fill is enabled. **The core owns the drag.** It
  extends along one axis, like Excel, and paints the target outline. *(Observed 2026-09-29: a
```

New:

```
- **The core paints the handle** at the bottom-right corner of the Selection's last range, in the
  selection overlay, when the Consumer declares that fill is enabled: for whole columns at the first
  row's top-right corner, and for whole rows at the first column's bottom-left, beside the Heading;
  for every cell, none ("The handle on a whole axis", 2026-10-12, below). **The core owns the
  drag.** It extends along one axis, like Excel, and paints the target outline. *(Observed 2026-09-29: a
```

### 2. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
  first rules are: copy, with relative References shifted; a linear series from two or more selected
  numbers; a series of dates by day. `Item 1` → `Item 2` and the rest of Excel's patterns come
  later, and each is refused until it arrives.
```

New:

```
  first rules are: copy, with relative References shifted; a linear series from two or more selected
  numbers; a series of dates by day; and, in a fill from whole columns or whole rows, a series by one
  from a single number in a line ("The handle on a whole axis", below). `Item 1` → `Item 2` and the
  rest of Excel's patterns come later, and each is refused until it arrives.
```

### 3. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
- **Item 15's cache of appearances drops every entry whose row instance a new Window does not hold.**
- **What it keeps for the rows it does hold is their appearance values.**
```

New:

```
- **Item 15's cache of appearances drops every entry whose row instance a new Window does not hold.**
- **What it keeps for the rows it does hold is their appearance values.**

## The handle on a whole axis *(2026-10-12)*

*(2026-10-12, decided with the user, on the seventeenth Windows run's h3: the fill handle on a whole
axis follows Excel; a range spanning every row puts its handle at its first row's top-right corner,
and one spanning every column at its first column's bottom-left. Ctrl+A, several whole columns or
rows, and which way the drag fills were to be asked next, with today's behaviour kept until then.)*
The core had put the handle at the bottom-right of every range, so a whole column's stood a million
rows down and a whole row's sixteen thousand columns across, and ExSheet showed none that could be
grabbed. The run pictured column C and row 3 (`verification/2026-10-11-windows-17/`, Part H, h3).
The readings of 2026-10-12 asked the rest (`verification/2026-10-12-windows-readings/`, R5).

- **Whole columns put the handle at the first row's top-right corner, and whole rows at the first
  column's bottom-left.** Every row and every column are the grid's extent
  (`SelectionRange.SpansEveryRow`, `SpansEveryColumn`). Several whole columns put it at the last
  column's top-right and several whole rows at the last row's bottom-left, as Excel did for columns
  B:D and rows 2:4, each selected from its first Heading (observed, R5 (2)). Where Excel puts it
  when the Extent is the first column or row was not read; ExGrid keeps the last.
- **Every cell (Ctrl+A) has none.** Excel showed none in view (observed, R5 (1)); whether it draws
  one at XFD1048576 was not read. Nothing is left to fill to, so the core paints none anywhere, at a
  small grid's bottom-right too (by generalisation).
- **It lies just inside the cells, beside the Heading.** It is centred on the edge that runs along
  the range, the column's right edge or the row's bottom edge, and lies inside the range across the
  other: its top is the first row's top, and its left the first column's left. Excel's straddles the
  Heading's bottom edge (observed, R5 (2)). The header and the Row Headings stand above the
  selection layer (ADR-0008, 2026-10-01), so a handle straddling their edge would be half covered;
  ExGrid's lies half its side lower, uncovered.
- **It stays in view.** With a whole column's first row scrolled above the readable area, the
  handle stands at the top-right of the part in view, beside the header, as Excel's stayed, clipped
  under its Heading (observed, R5 (5)). With a whole row's first column under the Row Headings or the
  Pinned Columns, it stands at the left of the part in view (by generalisation). ExGrid scrolls by
  the pixel, so the handle stands at the readable area's edge, placed at each render from the scroll
  the grid last read. A press is judged where the scroll it was taken at put the handle, so
  `PressTakenAt` carries the vertical scroll offset beside the horizontal one (ED-31; reading a
  scroll offset is ADR-0021's second entry).
- **The drag fills along the axis the range does not span.** A whole column's handle fills across,
  a whole row's up or down, and the Selection is then the source and the target, whole columns or
  rows; dragged along the axis the range spans, nothing is reached and nothing is raised (observed,
  R5 (3) and (4): C:E after the drag right, nothing after the drag down or sideways). The grab box
  is the handle's, twice its side about its centre, and a press on a Row Heading stays the
  Heading's, though a whole row's handle stands against it.
- **ExSheet fills a whole axis as Excel did.** Every line is filled: text copied, a Formula filled
  as one, and **a single number in a line goes on by one per cell** (observed, R5 (3) and (4): C1's
  `1` gave D1 `2` and E1 `3`, A3's `1` gave A4 `2` and A5 `3`), where one cell's handle copies its
  number (observed, R5's control: C5's `1` dragged down gave 1, 1). The series is held at 15
  significant digits as the trend is, and counts back by one filled left or up (by generalisation).
  A single number in any other source of several cells is still copied: Excel was not read there.
- **ExSheet writes a whole axis's format on the target axis.** A fill from whole columns writes each
  target column's format as the one its source column records, then only the lines that hold a
  cell; whole rows likewise. Each cell shows, and records as its own four sides (ADR-0071), what
  writing it would give it, since a cell's own part is its row's, else its column's; the format is
  recorded once on the column, as a whole column's is (ADR-0047). Excel's record of a fill from
  formatted whole columns was not read. Written cell by cell, column C bold filled into D:E was some
  two million records: `Check` 2,472 ms, `Do` 14,058 ms, 931 MB on the heap and a Sheet Document of
  79.6 million characters, Debug build on Linux; on the columns, 37 ms, 17 ms, 1 MB and 424
  characters.
```

### 4. docs/adr/0008-selection-is-painted-by-an-overlay.md

Old:

```
- **The fill handle stays where it was**, centred on the range's corner, which is now the outline's
  outer corner. Centring it on the outline's centre line would need the outline's width in C#.
  That is a stylesheet value, and pairing it with a C# constant is the defect
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) removes.
```

New:

```
- **The fill handle stays where it was**, centred on the range's corner, which is now the outline's
  outer corner. Centring it on the outline's centre line would need the outline's width in C#.
  That is a stylesheet value, and pairing it with a C# constant is the defect
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) removes.
  *(2026-10-12, decided with the user: whole columns put it at the first row's top-right corner and
  whole rows at the first column's bottom-left, as Excel does.)* There it lies just inside the
  cells, beside the Heading, which stands above this layer; Excel's straddles the Heading's edge
  (the readings of 2026-10-12, R5). Every cell has none, and a whole column's handle stays at the
  top of the part in view. [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), "The handle on
  a whole axis".
```

### 5. docs/definition-of-done.md

Old:

```
| **DC-12** | MUST | With the fill handle declared, it is painted at the bottom-right of the Selection's last range, in the selection overlay; a disjoint Selection shows none (ADR-0050, item 5; ADR-0008) | Layer 2 | one handle element, positioned by the overlay; none for a disjoint Selection |
```

New:

```
| **DC-12** | MUST | With the fill handle declared, it is painted at the bottom-right of the Selection's last range, in the selection overlay; for a range spanning every row (whole columns) at its first row's top-right corner, and for one spanning every column (whole rows) at its first column's bottom-left, beside the Heading, held at the readable area's edge while that row or column is scrolled away; a range spanning both, and a disjoint Selection, show none (ADR-0050, item 5 and "The handle on a whole axis"; ADR-0008) | Layer 2 + Layer 3 | one handle element, positioned by the overlay at the corner the range's shape names; none for every cell or a disjoint Selection; on `/sheet`, a whole column's and a whole row's handle painted beside the Heading and uncovered, and a whole column's at the top of the part in view with row 1 scrolled away |
```

Decision (1)'s branch (`claude/run17gh-focus-among-ranges`) may rewrite DC-12's "a disjoint Selection
shows none" too; the two touch different clauses of the one row.

### 6. docs/definition-of-done.md

Old:

```
| **SH-15** | MUST | Fill: copy with References shifted, a linear series from two or more numbers, dates by day, each as Excel; any other pattern refused, never filled with copies (ADR-0050) | Layer 1 | as stated |
```

New:

```
| **SH-15** | MUST | Fill: copy with References shifted, a linear series from two or more numbers, dates by day, and from whole columns or whole rows a series by one from a single number in a line, each as Excel; any other pattern refused, never filled with copies; a fill from whole columns or whole rows records its source's column (row) format on the target's columns (rows) and writes only the cells that hold something (ADR-0050, "The handle on a whole axis") | Layer 1 | as stated |
```

### 7. docs/specs/exsheet/issues/24-selection-looks-like-excel.md

Old:

```
The run noted above as written and not yet run has run since, and UX-9 passes. Two things stay: the
three readings under "To observe on the next Windows run", which no run has taken yet (ADR-0008
still says "Read, not observed"), and the re-run of the selection-hole measurement, still pending in
its README.
```

New:

```
The run noted above as written and not yet run has run since, and UX-9 passes. Two things stay: the
three readings under "To observe on the next Windows run", which no run has taken yet (ADR-0008
still says "Read, not observed"), and the re-run of the selection-hole measurement, still pending in
its README.

2026-10-12, question 3 read in the seventeenth Windows run (h3) and in the readings of 2026-10-12
(R5): Excel puts a whole column's handle at its top-right, beside its Heading, a whole row's at its
bottom-left, several whole columns' at the last one's top-right, and shows none in view for every
cell; ExGrid follows (`claude/run17gh-fill-handle-axis`, ADR-0050 "The handle on a whole axis"). The
outline and the untinted first cell were as ExSheet's already.
```

### 8. samples/ExGrid.Docs/Pages/Grid/GridEditing.razor

Old:

```
                <strong>The fill handle</strong>, with <code>ShowFillHandle</code>, is the square at the corner of the
                selection. Drag it and the grid raises <code>OnFill</code> with the source range and the target
                beside it; what a fill means — a copy, a series — is yours.
```

New:

```
                <strong>The fill handle</strong>, with <code>ShowFillHandle</code>, is the square at the corner of the
                selection: its bottom-right, or the top-right of whole columns and the bottom-left of whole rows, one
                or several, beside the header or the Row Headings, as Excel puts it (read beside Excel on 2026-10-11
                and 2026-10-12). It stays at the edge of the part in view when that first row or column is scrolled
                away, and a selection of every cell has none. Drag it and the grid raises <code>OnFill</code> with
                the source range and the target beside it; what a fill means — a copy, a series — is yours.
```

### 9. samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor

Old:

```
            Beyond typing, the user gets what a spreadsheet user expects: the fill handle (Formulas with
            their References shifted, a series from two or more numbers, dates by day — any other pattern is
            refused, with the reason), Ctrl+D and Ctrl+R, Delete to clear contents and keep formats, Ctrl+F to
```

New:

```
            Beyond typing, the user gets what a spreadsheet user expects: the fill handle (Formulas with
            their References shifted, a series from two or more numbers, dates by day — any other pattern is
            refused, with the reason; for whole columns or rows it stands beside the Heading and fills every
            row or column, a single number going on by one, as Excel's did beside it on 2026-10-12), Ctrl+D
            and Ctrl+R, Delete to clear contents and keep formats, Ctrl+F to
```

### 10. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

Old:

```
            A format lives at three levels, as in Excel: cell over row over column. A format set on whole columns or
            whole rows is recorded once on them, and covers the cells that hold nothing as well.
```

New:

```
            A format lives at three levels, as in Excel: cell over row over column. A format set on whole columns or
            whole rows is recorded once on them, and covers the cells that hold nothing as well. A fill from whole
            columns or whole rows carries it the same way, recorded once on the columns or rows it fills.
```

**The Examples need nothing.** `EditingGrid` and `EditingGridMud` declare `ShowFillHandle`; Ctrl+Space
in them shows the handle beside the header, as the new sentence says, and Ctrl+A shows none. The API
table is generated from `ShowFillHandle`'s XML comment, which this branch rewrites. The site was run
from this branch (`dotnet run --project samples/ExGrid.Docs`, Chromium under xvfb) and the editing
Example used once: a cell of row 2, Ctrl+Space, the handle at `top: 0px` beside the header; the grid
scrolled 84 px (row 4 at the top) and 98 px (mid-row), the handle's top on the header's bottom edge
both times; Ctrl+A, no handle; `/exgrid`'s API table carries the new `ShowFillHandle` text; no console
error or warning. The three page edits above are prose and were not applied for that run; the
orchestrator runs the site again once they are.

## Change Notes

- `docs/releases/unreleased/run17gh-fill-handle-axis-exgrid.md`: ExGrid, Behaviour,
  `exgrid/editing#bulk-changes--delete-and-undo`.
- `docs/releases/unreleased/run17gh-fill-handle-axis-exsheet.md`: ExSheet, Fixed, `exsheet#a-first-sheet`.

## When `main` is merged in

`main`'s #89 (ADR-0201) lists `PressTakenAt` in `src/ExGrid/PublicAPI.Unshipped.txt`. Its line becomes
`ExGrid.Components.ExGrid<TRow>.PressTakenAt(string! kind, double offsetX, double offsetY, int firstRow, double scrollLeftPx, double scrollTopPx, int rowSequence, int layout) -> void`.
`FillHandleCorner` and `FillRules.HandleCorner` are internal. #89 also touched `ExGrid.razor`,
`FillRules.cs` and `GridEditing.razor`; the edits above are checked against this branch's tip.

## Questions for the next Windows run

Each in a new workbook at 100% zoom, the pictures cut to the cells and the Headings, as R5's were.

1. **Every cell, at the far corner.** Ctrl+A on an empty sheet, then `XFD1048576` in the Name Box
   and Enter, Ctrl+A again, Ctrl+Backspace: is a handle drawn at XFD1048576? Decides whether "none
   anywhere" stays (any drawn there would be a difference to note; it could not be dragged anywhere).
2. **The Extent at the first column.** Press D's Heading, Shift+press B's: is the handle at D's
   top-right (the last column, as ExGrid has it) or B's? The same for rows 4 then 2.
3. **A whole row with column A scrolled away.** Row 3 from its Heading, then the horizontal scroll
   bar's right arrow three times: is the handle drawn at the left of the part in view (as ExGrid has
   it, by generalisation), or gone?
4. **A lone number in a range that is not a whole axis.** C1 `1`, C2 `5`; select C1:C2 and drag the
   handle two columns right: D1:E1 `1 1` or `2 3`, D2:E2 `5 5` or `6 7`? Then C1 `1` with C2 empty,
   the same. Decides whether ExSheet's series by one reaches every source of several cells (today
   only whole columns or rows; one cell copies, as R5's control showed).
5. **A whole axis with a lone number beside a blank in a line.** Columns B:C with B1 `1` and C1
   empty, dragged right to E: what do D1 and E1 hold? ExSheet refuses ("numbers mixed with other
   cells"); a value there is a pattern to implement.
6. **A whole column's format.** C1 `1`, C:C bold (Ctrl+B on C's Heading), dragged right to E: D100
   and E100 bold? Then insert a row at row 50 (row 50's Heading, Ctrl++): is D50 bold? Bold there
   says the format is the column's, as ExSheet records it; not bold says Excel wrote cells.
7. **Left and up, and a date.** Column C (C1 `5`) dragged left to A: B1 `4`, A1 `3`? Column C with
   C1 `9/26/2026`, dragged right: D1 `9/27/2026`? Both are ExSheet's by generalisation.
