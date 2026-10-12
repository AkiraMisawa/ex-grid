# Run 17 G-H, the fill handle on a whole axis: what the orchestrator writes

Branch `claude/run17gh-fill-handle-axis`, from `main` at `a533de01`. Decision (2) of 2026-10-12, taken with the user
on the seventeenth Windows run's h3 (`verification/2026-10-11-windows-17/report.md` on
`claude/exsheet-windows-verify-17`, Part H; `part-h/shots/h-h3-1-corner-zoom.png`, `h-h3-2-corner-zoom.png`):

> The fill handle on a whole axis follows Excel: a range spanning every row puts its handle at its first row's
> top-right corner, one spanning every column at its first column's bottom-left. Ctrl+A, several whole columns or
> rows, and which way the drag fills are asked next run, and keep today's behaviour until then.

Nothing here edits an ADR, `CONTEXT.md`, the Definition of Done or the Docs Site: the text for each is below, in the
repository's voice, as whole lines in the target's own indentation.

## What was done

| Where | Was | Now |
|---|---|---|
| ExGrid, `FillRules.HandleCorner` (internal, new) | the handle at the range's bottom-right, whatever its shape | bottom-right; top-right for a range spanning every row and not every column; bottom-left for one spanning every column and not every row; a range spanning both (Ctrl+A) keeps the bottom-right |
| ExGrid, `SelectionStyles.Handle`, `HandleCell`, `HandleCentrePx` | centred on the bottom-right corner, in that corner's layer, while its row is painted | centred on the corner at the bottom-right; at the top-right, centred on the right edge with its top on the first row's top; at the bottom-left, centred on the bottom edge with its left on the first column's left. The layer and the painted-rows test follow the corner's cell |
| ExGrid, `IsOnFillHandle` (the hit test) | a grab box twice the handle's side about the bottom-right corner | the same box about the handle's centre, at its corner; a press in the Row Headings is never on the handle, though a whole row's handle stands against them |
| ExGrid, the drag | from the bottom-right, along one axis | unchanged, from wherever the handle stands: a whole column has no rows past it and fills across; a whole row fills up or down |
| ExSheet.Engine, `Sheet.PlanFill` | every line of the source filled: a whole column C filled into D wrote a state over all 1,048,576 rows. Debug build, .NET 10 JIT on this container: `Check` 1,235 ms, `Do` 3,339 ms, the managed heap 715 MB afterwards | only the lines that can write anything (`LinesToFill`): a line blank in the source and the target, with no row or column format over any of its cells, wrote a blank state over cells that record nothing, which changes nothing and refuses nothing. The same fill: `Check` 20 ms, `Do` 24 ms, 8 MB. A row (column) format the fill runs along reaches every line, and then every line is filled, as before |

**Where the handle lies about the corner is the picture's, not a reading.** In both shots the handle lies wholly in the
cells beside the Heading: under C's Heading its top is the Heading's bottom edge and it is centred on the column's right
edge; beside row 3's Heading its left is the Heading's right edge and it is centred on the row's bottom edge. ExGrid's
header and Row Headings stand above the selection layer (ADR-0008, 2026-10-01), so a handle centred on the corner would
have half of it beneath them, or past the grid's edge without Headings. Only Excel's 100% zoom was pictured.

**Why the engine change is here.** The handle a million rows down could hardly be dragged, so a fill from whole columns
never ran. Beside the Heading it is one drag away, and on the WebAssembly host the old plan would have run the million
lines in the browser's interpreter. What is written is unchanged; the engine tests below pin the three cases where a
blank line does write (a format along the fill), and each fails with the fallback taken out.

### Tests, each named with its ADR

- **Layer 2**, `tests/ExGrid.Components/FillHandleTests.cs`, nine new (25 in the class): a whole column's handle at
  `left: 197px; top: 0px` (Ctrl+Space); several whole columns at the last one's top-right (Ctrl+Space, Shift+→); a whole
  row's at `left: 0px; top: 37px` (Shift+Space); beside 40 px Row Headings at `left: 40px`, and a press on row 2's Heading
  inside the grab box selects row 2 and starts no fill (fails without the lead test); the handle gone with row 0 scrolled
  out of the painted rows; Ctrl+A over five rows at the bottom-right; a whole column's handle dragged down (nothing
  reached) then across (one `OnFill`, `Right`, `Source` 0..49 × Book, `Target` 0..49 × Amount, the handle then at the
  second column's top-right); a whole row's dragged across (nothing) then down (one `OnFill`, `Down`); a press at a whole
  column's old bottom-right corner selects. Layer 1 has no new test: `HandleCorner` is internal and ExGrid grants no
  `InternalsVisibleTo` (ADR-0201 is giving it one; the corner's four cases can move to `FillRuleTests` then).
- **Engine**, `tests/ExSheet.Engine.Tests/FillTests.cs`, five new: C:C filled into D:D copies C1, C2, `=C1+C2` and
  C1048576 and clears D9's text, and its undo gives the document back byte for byte; row 2 filled into rows 3:4 across all
  16,384 columns; a source column's format carried into its blank rows; a target column's format, and down a target
  row's, written over in the blank lines.
- **Layer 3**: `selection-look.spec.mjs`, `DC-12 (built-in|MudBlazor Chrome)`: column C from its Heading, the handle
  centred on C1's right edge with its top on C1's top, not above the Heading's bottom, and its middle pixel the handle's
  own colour; row 3 from its Heading, the handle's left on A3's left, centred on A3's bottom, right of the Heading,
  uncovered. `declarations.spec.mjs` (where the fill cases are; `sheet.spec.mjs` defers them there): column E's handle
  dragged to F3 fills F2:F4 (`1`, `2`, `=F2+F3`) and nothing in G2 or F5, and one Ctrl+Z takes it back; row 6's handle
  dragged to D8 fills C7:D8 (`5`, `=C8*2`), and one Ctrl+Z takes it back.

## ADR-0050, section 5: the first bullet's opening

Replace

```
- **The core paints the handle** at the bottom-right corner of the Selection's last range, in the
  selection overlay, when the Consumer declares that fill is enabled. **The core owns the drag.** It
  extends along one axis, like Excel, and paints the target outline. *(Observed 2026-09-29: a
```

with

```
- **The core paints the handle** at the bottom-right corner of the Selection's last range, in the
  selection overlay, when the Consumer declares that fill is enabled: for whole columns at the first
  row's top-right corner, and for whole rows at the first column's bottom-left, beside the Heading
  ("The handle on a whole axis", 2026-10-12, below). **The core owns the drag.** It extends along
  one axis, like Excel, and paints the target outline. *(Observed 2026-09-29: a
```

## ADR-0050: a new section at the end

```
## The handle on a whole axis *(2026-10-12, decided with the user)*

The seventeenth Windows run read where Excel puts the handle of a whole column and of a whole row
(`verification/2026-10-11-windows-17/`, Part H, h3; `part-h/shots/h-h3-1-corner-zoom.png` and
`h-h3-2-corner-zoom.png`). Column C selected from its Heading: the handle at the column's top-right
corner, beside C's Heading. Row 3 from its Heading: at the row's bottom-left corner, beside row 3's.
The core put it at the bottom-right, as for any range, so a whole column's stood a million rows
down and a whole row's sixteen thousand columns across, and ExSheet showed none that could be
grabbed.

- **A range spanning every row puts its handle at its first row's top-right corner, and one
  spanning every column at its first column's bottom-left.** Every row and every column are the
  grid's extent (`SelectionRange.SpansEveryRow`, `SpansEveryColumn`), so several whole columns put
  it at the last column's top-right and several whole rows at the last row's bottom-left. Excel was
  pictured with one; several are asked next.
- **It lies wholly beside the Heading, as Excel's does.** It is centred on the edge that runs along
  the range, the column's right edge or the row's bottom edge, and lies inside the range across the
  other: its top is the first row's top, and its left the first column's left. Centred on the
  corner, half of it would lie beneath the header or the Row Headings, which stand above the
  selection layer (ADR-0008, 2026-10-01), or past the grid's edge where there are none.
- **The drag is unchanged.** It extends along one axis, as item 5 says. A whole column has no rows
  past it, so its handle fills across; a whole row's fills up or down. The grab box is the
  handle's, twice its side about its centre, and a press on a Row Heading stays the Heading's,
  though a whole row's handle stands against it.
- **Every cell (Ctrl+A), a range spanning both, keeps the bottom-right corner** until a Windows run
  has read where Excel puts that one.
- **It goes with its corner's row.** A whole column scrolled so that its first row is out of the
  painted rows shows no handle, as a bottom-right corner scrolled away shows none (ADR-0053). What
  Excel shows there is asked next.
- **ExSheet's fill costs the lines that hold something.** A fill from whole columns is a million
  lines, and filling each wrote a blank state over every blank cell: on the server, seconds and
  some 700 MB before anything showed, and on WebAssembly the same work in the browser. A line that
  is blank in the source and the target, with no row or column format over any of its cells, wrote
  nothing, and is now left out, so what a fill writes is unchanged.
```

## ADR-0008: the handle bullet of "Settled while building" (2026-10-01)

Replace

```
- **The fill handle stays where it was**, centred on the range's corner, which is now the outline's
  outer corner. Centring it on the outline's centre line would need the outline's width in C#.
  That is a stylesheet value, and pairing it with a C# constant is the defect
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) removes.
```

with

```
- **The fill handle stays where it was**, centred on the range's corner, which is now the outline's
  outer corner. Centring it on the outline's centre line would need the outline's width in C#.
  That is a stylesheet value, and pairing it with a C# constant is the defect
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) removes.
  *(2026-10-12, decided with the user: whole columns put it at the first row's top-right corner and
  whole rows at the first column's bottom-left, each lying wholly beside its Heading, as Excel's
  does, and centred on the range's edge along the other side; every cell keeps the bottom-right.
  [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), "The handle on a whole axis".)*
```

## Definition of Done, DC-12

Replace

```
| **DC-12** | MUST | With the fill handle declared, it is painted at the bottom-right of the Selection's last range, in the selection overlay; a disjoint Selection shows none (ADR-0050, item 5; ADR-0008) | Layer 2 | one handle element, positioned by the overlay; none for a disjoint Selection |
```

with

```
| **DC-12** | MUST | With the fill handle declared, it is painted at the bottom-right of the Selection's last range, in the selection overlay; for a range spanning every row (whole columns) at its first row's top-right corner, and for one spanning every column (whole rows) at its first column's bottom-left, wholly beside the Heading; a range spanning both keeps the bottom-right; a disjoint Selection shows none (ADR-0050, item 5 and "The handle on a whole axis"; ADR-0008) | Layer 2; Layer 3 for the Headings | one handle element, positioned by the overlay at the corner the range's shape names; none for a disjoint Selection; on `/sheet`, a whole column's and a whole row's handle painted beside the Heading and uncovered |
```

Decision (1)'s area may rewrite DC-12's "a disjoint Selection shows none" too; the two edits touch different clauses.

## Ticket 24 (`docs/specs/exsheet/issues/24-selection-looks-like-excel.md`)

Question 3 is answered for the handle. A comment under "Comments", for whoever closes the ticket's readings:

```
2026-10-12, question 3 read in the seventeenth Windows run (h3): Excel puts a whole column's handle at
its top-right, beside its Heading, and a whole row's at its bottom-left; ExGrid follows
(`claude/run17gh-fill-handle-axis`, ADR-0050 "The handle on a whole axis"). The outline and the
untinted first cell were as ExSheet's already.
```

## Docs Site

- **`/exgrid/editing`**, "Bulk changes, Delete and Undo" (`Pages/Grid/GridEditing.razor`, lines 67 to 69). Replace

  ```
                <strong>The fill handle</strong>, with <code>ShowFillHandle</code>, is the square at the corner of the
                selection. Drag it and the grid raises <code>OnFill</code> with the source range and the target
                beside it; what a fill means — a copy, a series — is yours.
  ```

  with

  ```
                <strong>The fill handle</strong>, with <code>ShowFillHandle</code>, is the square at the corner of the
                selection: its bottom-right, or, as in Excel, the top-right of whole columns and the bottom-left of
                whole rows, beside the header or the Row Headings, where it can be reached. Drag it and the grid
                raises <code>OnFill</code> with the source range and the target beside it; what a fill means — a
                copy, a series — is yours.
  ```

- **`/exsheet`**, "A first sheet" (`Pages/Sheet/SheetOverview.razor`, lines 50 to 52). Replace

  ```
            Beyond typing, the user gets what a spreadsheet user expects: the fill handle (Formulas with
            their References shifted, a series from two or more numbers, dates by day — any other pattern is
            refused, with the reason), Ctrl+D and Ctrl+R, Delete to clear contents and keep formats, Ctrl+F to
  ```

  with

  ```
            Beyond typing, the user gets what a spreadsheet user expects: the fill handle (Formulas with
            their References shifted, a series from two or more numbers, dates by day — any other pattern is
            refused, with the reason — standing beside the Heading for whole columns and whole rows, as
            Excel's does), Ctrl+D and Ctrl+R, Delete to clear contents and keep formats, Ctrl+F to
  ```

- **The Examples need nothing.** `EditingGrid` and `EditingGridMud` declare `ShowFillHandle`; Ctrl+Space in them now
  shows the handle beside the header, as the new sentence says. The API table is generated from `ShowFillHandle`'s XML
  comment, which this branch extends with the two corners.

Run the site once with the two sentences in (`nix develop -c dotnet run --project samples/ExGrid.Docs`), Ctrl+Space in
the editing Example, and see the handle beside the Amount header.

## Change Notes

- `docs/releases/unreleased/run17gh-fill-handle-axis-exgrid.md`: ExGrid, Behaviour, `exgrid/editing#bulk-changes--delete-and-undo`.
- `docs/releases/unreleased/run17gh-fill-handle-axis-exsheet.md`: ExSheet, Fixed, `exsheet#a-first-sheet`.

## Questions for the next Windows run

Each in a new workbook at 100% zoom, en-GB, the pictures cut to the cells and the Headings, as Part H's were. Beside
each, ExSheet on `/sheet` (WebAssembly, Chrome) with the same gestures.

1. **Ctrl+A.** Click A1 of an empty sheet, press Ctrl+A (every cell). Read: is a fill handle drawn in view, and where
   (beside the Headings' corner, at A1's top-left, at the window's bottom-right)? Then type `XFD1048576` into the Name
   Box and Enter, Ctrl+A again, and press Ctrl+Backspace (shows the active cell without moving the Selection): read the
   corner at XFD1048576. Decides: a handle Excel draws in view is where ExGrid's goes for every cell; none in view, or
   only at the far corner, keeps today's bottom-right.
2. **Columns B:D and rows 2:4.** Press B's Heading, Shift+press D's: read where the handle is (D's top-right, B's, or
   none). Press row 2's Heading, Shift+press row 4's: read where (row 4's bottom-left, row 2's, or none). Then press D's
   Heading and Shift+press B's (the Extent to the left), and row 4's then row 2's: does the handle move with the Extent?
   Decides: the last column (the last row) as built, or the Extent's, or the first.
3. **What a whole column's and a whole row's drag fills.** C1 `1`, C2 `2`, C3 `=C1+C2`, C5 `Item 1`, C:C bold (Ctrl+B
   on C's Heading). Select C from its Heading and drag its handle right to E (two columns). Read D1:E5's values, D3's
   and E3's Formulas, and whether D:E are bold (cell by cell at D1, D100, E100). Undo, then drag C's handle down: does
   anything happen? Then A8 `5`, B8 `=A8*2`; select row 8 from its Heading, drag its handle down two rows; read
   A9:B10; undo; drag it right: anything? Decides: ExSheet copies each row, refuses the whole fill over `Item 1`, and
   writes C's format into every row of D:E; Excel continuing `Item 2` or recording the format on the column instead is
   a difference to correct; a drag along the whole axis that does something in Excel is a difference for ADR-0050.
4. **A whole column with row 1 scrolled away.** Select C from its Heading, then scroll with the wheel until row 1 is
   out of view (row 40 at the top). Read: is the handle drawn anywhere (held beside C's Heading at the top of the
   readable area, or gone)? Then the same for row 3 scrolled left out of view (column Z at the left). Decides: gone
   with its row, as ExGrid has it, or held beside the Heading, which would make the handle follow the readable area's
   edge, a change to ADR-0050.

## What this branch did not touch

- Ctrl+A, several whole columns or rows, and which way the drag fills keep today's behaviour, as decided: Ctrl+A's
  handle stands at the bottom-right; several whole columns put it at the last column's top-right (the rule for one,
  unchanged); the drag is ADR-0050's one axis.
- Decision (1) (the Focus among several ranges, and no fill handle there) is another branch's; a disjoint Selection
  already shows no handle here, unchanged.
