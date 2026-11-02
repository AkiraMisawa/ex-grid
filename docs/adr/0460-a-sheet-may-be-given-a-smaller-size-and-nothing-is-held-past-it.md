# A Sheet may be given a smaller size, and nothing is held past it

*Decided with the user on 2026-11-02, in the project thread that answers
[issue #511](https://github.com/AkiraMisawa/ex-grid/issues/511). The user chose to put it in the next release, 0.2.0, and chose both
options recommended below: the size is an option of the ExSheet, and a Reference past it reads the empty cell. It rewrites
the second half of [ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)'s
"One row height, and Excel's full extent", which said a Sheet always shows 1,048,576 rows × 16,384 columns.*

## Context

ADR-0046 gave every Sheet Excel's full extent, held sparsely, so that an empty Sheet costs nothing and keys,
paste, fill and insertion meet the edge where Excel meets it. Excel has no smaller worksheet. A Consumer asked
for one on 2026-11-01 (issue #511): a grid of ten rows by ten columns, `A` to `J` and `1` to `10`.
Today the nearest answer is ExGrid with ten columns, which has no formulas, or an ExSheet the user can scroll a
million rows past those ten.

Google Sheets is the precedent: each sheet has a number of rows and columns, 1,000 × 26 when it is new, and
nothing lies past them.

Two things were the user's to decide, and are recorded under "The two choices" below: where the size lives, and what
a Reference past it reads.

## Decision

**A Consumer may give a Sheet Size: fewer rows than 1,048,576, fewer columns than 16,384, or both.** Without one,
a Sheet is Excel's full extent and nothing in this ADR changes anything. A size larger than Excel's, or one of
less than one row or one column, is refused by name.

**The size is an option of the ExSheet, for every Sheet it shows, and is not saved** (`sheetSize` on `createSheet` and
React's `ExSheet`, `SheetSize` on Blazor's). A Sheet added from the Sheet Tabs takes it. Either count may be given
alone, the other staying Excel's — `{ rows: 10 }` is ten rows of every column — because limiting only the rows is the
common case, and a Consumer made to write 16,384 to mean "as Excel" is a Consumer that can mistype it (asked by the
user, 2026-11-02).

**The size is a boundary on content, not on the address space.** A Sheet still addresses `A1` to `XFD1048576`,
its formulas are Excel's formulas, and its `.xlsx` is Excel's `.xlsx`. What the size changes is where a cell may
hold something, and what the user can reach:

- **The grid draws the size and nothing past it.** A Sheet of 10 × 10 has ten rows and the columns `A` to `J`;
  below and to the right of them is the empty Viewport any short ExGrid shows. The Row Headings run 1 to 10.
- **Every way of moving stops at its edge**, as Excel's stop at `XFD1048576`: arrows, Ctrl+arrow, Ctrl+End,
  Page Down, Ctrl+A, a click on a Heading (which selects that column's or row's cells inside the size), a drag
  that leaves the rows. An address typed in the Name Box that lies past it is refused by name.
- **Every write that would put content past it is refused by name, never clipped**, by the refusals that
  already guard Excel's edge, naming this edge in place of `XFD1048576`: a paste or a fill whose block runs
  past it (`blockWouldLeaveSheet`), and an insertion of rows or columns that would push a cell holding an Entry
  past it (`entriesWouldLeaveSheet`). A fill drag cannot be dragged past it. The engine holds every edit to it, not
  only the ones the screen can make: an edit whose cells run past the edge is refused (`blockWouldLeaveSheet`),
  whole columns counted by their columns and whole rows by their rows, so a width set on column `K` of a 10 × 10
  Sheet is refused and one set on `A:J` is taken.
- **A spilled array that would pass it shows `#SPILL!`**, as one that would pass Excel's edge does
  ([ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md)).
- **A document that holds an Entry past it is refused when it is opened**, by name, with the first such cell;
  a Sheet Document and an `.xlsx` alike. It is never opened with
  the cells past the edge left out, because those cells would still be read by formulas and saved again, out of
  the user's sight. A Cell Format alone past the edge is not refused: it holds nothing to read, and Excel files
  carry whole-column formats routinely.
- **A Sheet Size handed in later that would leave an Entry outside is refused by name**, and the Sheet keeps the
  size it had. A smaller one is refused while a cell is being edited, as a command is, since the edit may stand past
  it; once taken, it empties the undo stack, whose steps could put an Entry back past the new edge, as a document
  opened empties it. A larger one keeps the stack.
- **The row-height ceiling counts the Sheet's own rows.** ADR-0046 refused a row height at which 1,048,576 rows
  pass the browser's layout ceiling; a smaller Sheet is refused only when its own rows would.

A Sheet Size is not a cap on what ExGrid can draw, and it is not hidden rows or columns: Excel's hidden rows are
document data a user can unhide, and remain unsupported.

## The two choices

1. **Where the size lives: an option of the ExSheet, for every Sheet it shows, not saved** (chosen). A file saved from
   a 10 × 10 Sheet and opened in Excel is an ordinary worksheet with content in `A1:J10`. It is what issue #511 asks,
   it is the Consumer's decision about the screen like `showRowHeadings`, and Excel's `.xlsx` has nowhere to keep it.
   - *Not chosen: each Sheet's own, saved in the Sheet Document*, as Google Sheets keeps it. Sheets of one Workbook
     could differ, and an `.xlsx` would lose it on the way through Excel. It leads on to the user changing the size,
     which would be a command of its own.
2. **What a Reference past the size reads: the empty cell it names, as it does on a Sheet of Excel's size** (chosen).
   `=K1` reads empty in a 10 × 10 Sheet, and `A:A` is all of column `A`. The cell exists and is empty, and nothing can
   ever be put in it, so the answer is true; every Formula means what it means in Excel, and the file opens in Excel
   unchanged.
   - *Not chosen: `#REF!`*, with `A:A` meaning `A1:A10`. The address space itself would shrink: every Reference
     checked against the size, and a Formula moved or copied into a Sheet of another size changing its meaning.

## Consequences

- `Workbook` in `@ex-grid/sheet-engine` holds a `SheetSize`, which every Sheet's refusals, structural edits and
  spill layout test against, and which decides how a range fits it. `rowCount` and `columnCount` stay Excel's, the
  address space.
- The refusal of a paste that runs past the edge names the Sheet's last cell: its words hold `{0}` in place of
  `XFD1048576`, in ExSheet's English and Japanese and in .NET's `SheetCommandIds.EnglishFor`.
- `SheetSource` reports the size's rows, and `SheetColumns` builds the size's columns; the Blazor adapter's check
  that a Sheet's columns are `A` to `XFD` reads the size.
- The Docs Site's ExSheet page says it, with an Example of a 10 × 10 Sheet; a Change Note says it is added.
- **Rejected: a Consumer-side cap on the view alone**, the grid drawing ten rows over a Sheet that still held a
  million. A paste, a spill or an insertion would then put content where nobody can see it, and the formulas would
  still read it: a figure moved out of sight but still counted, which is the first rule of the spine broken.
