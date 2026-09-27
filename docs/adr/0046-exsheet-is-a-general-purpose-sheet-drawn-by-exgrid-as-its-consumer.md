# ExSheet is a general-purpose sheet, drawn by ExGrid as that grid's Consumer

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet. It settles the
question [ADR-0019](./0019-one-repository-many-packages.md) left open — "Is ExSheet a sibling of
ExGrid, or a Consumer of it?" — and the reserved row in the Definition of Done's §21.)*

**ExSheet is a general-purpose spreadsheet**, not a component shaped around one screen's data. It
holds a **Sheet** of cells addressed `A1`, the **Entries** a user put into them, and a formula
engine that computes their **Values**. **It shows them by rendering one ExGrid, as that grid's
Consumer**: ExSheet holds and computes, ExGrid paints, selects, navigates and reports.

```
application  (ExSheet's Consumer: hands over and persists the Sheet Document, supplies Linked Tables)
   ↓ Sheet Document / Linked Tables          ↑ changes
ExSheet      (holds the Sheet, computes Values — ExSheet.Engine)
   ↓ pushes a Window of sheet rows            ↑ Edit Intents, paste, fill, ...
ExGrid       (painting, Selection, Focus, keyboard, clipboard, Chrome seams)
```

## Why a sheet at all, when the real Excel sits next to it

`CONTEXT.md` gives this as the reason ExGrid has no formula engine: "with the real Excel sitting
next to it, there is no reason to reimplement a worse formula engine". That sentence still holds
for ExGrid. ExSheet has to answer it, and does so on two grounds:

- **It is inside the application.** No file goes out and comes back. The application decides where
  the Sheet Document is stored, who may open it, and which cells may be written
  ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
- **Its Formulas can read the application's own data, live** — through **Linked Tables**
  ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)). This is the thing the
  real Excel next to it cannot do without an export, and it is why ExSheet is worth building.

## Why a Consumer, not a sibling

Three shapes were on the table in ADR-0019:

- **A Consumer of ExGrid** — chosen.
- **A shared kernel extracted into a package both use** — rejected for now. Nothing yet shows a
  part of ExGrid that ExSheet needs and a Consumer cannot reach. The changes ExSheet does need are
  few and each is an opt-in declaration on ExGrid
  ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)); if one ever cannot be expressed that
  way, the kernel question reopens with a concrete case.
- **A separate implementation** — rejected. It would duplicate virtualisation, Selection, the
  keyboard, the clipboard and every Chrome seam, and the two copies would drift.

ADR-0007 already made the grid report an Edit Intent and wait for the Consumer to push back new
rows, and ADR-0019 observed that ExSheet is "precisely a Consumer that owns a mutable cell model".
The push interface is what makes this possible without a kernel.

How ExSheet maps a Sheet onto ExGrid:

- **Columns `A` … `XFD` are Columns ExSheet builds.** Each one's accessor returns that sheet column's
  Value from the row. ExGrid already virtualises columns
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)), so 16,384 of them cost only what is on
  screen. Their headers are the **Column Headings**.
- **A Window is a run of sheet rows**, each a sparse set of cells. An edit hands back a new
  instance for every row whose Values changed, which is ADR-0007's third link.
- **Row numbers are the Row Headings**, a band ExGrid paints beside the rows, outside the column
  index space (ADR-0050).

## Rows and columns are places, not things

In a Sheet, "row 5" is a position, and it stays row 5 whatever is put into it. **Inserting a row
at 5 changes the contents of every row from 5 downwards; it does not move any row.** ExGrid
therefore sees a change of values, not a change of order. The Row Sequence Version does not move,
and the Selection stays where it was in index space
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)). That is
what Excel does: after inserting at row 5, the selection is on the new, empty row 5. Columns work
the same way. `A`…`XFD` are positions, so inserting a column changes values and leaves the set of
visible columns as it was.

The alternative was rows as objects that move when a row is inserted above them. That bumps the
Row Sequence Version on every insertion. The Selection would be dropped each time, which is the
friction ADR-0019 recorded, and ExGrid's core would have to change to carry it across.

**What this rules out: ExGrid's own sort and filter are not used on a Sheet.** They reorder a view
of rows the Consumer owns. In Excel, sorting is a command that rewrites cells, and AutoFilter
hides rows. When ExSheet gets either, it is one of ExSheet's own commands.

## One row height, and Excel's full extent

**Every row of a Sheet has the same height.** ExGrid's virtualisation positions a row at
index × row height ([ADR-0013](./0013-fixed-row-height.md)), and so do the selection overlay and
the editor. Excel lets each row have its own height, and grows a row when its text wraps. **ExSheet
does not, and this is a known difference from Excel**, not an omission. Heights per row would mean
rewriting ADR-0013: a prefix sum over a million rows, and every piece of geometry that relies on
the multiplication. A Consumer who asks for this is the trigger to reopen it, with measurements.

**A Sheet shows Excel's full extent: 1,048,576 rows × 16,384 columns**, held sparsely. The browser
can scroll 2^25 px, and ExGrid refuses a result taller than that by name rather than clipping it
(VZ-8). 1,048,576 rows fit at any row height up to 31 px; the default is 28 px. **A row height at
which the full extent does not fit is refused by name.** The Sheet is not quietly made shorter.

## One Sheet for now

An ExSheet holds one Sheet. **References already carry the Sheet** (`Sheet2!A1`), both in the
grammar and in how the engine records them
([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)). A
workbook of several Sheets can then be added without changing what anyone has written. When it
comes, **the engine belongs to the workbook, not to an ExSheet instance.** Two ExSheets on one
page showing two Sheets of one workbook share one dependency graph, because inserting a row in one
Sheet has to rewrite the References to it in the other.

## Consequences

- **ADR-0019's open question is closed**, and the package layout gains `ExSheet.Engine` beside
  `ExSheet` (ADR-0047). The reference direction stays one-way: `ExSheet` → `ExSheet.Engine`, and
  `ExSheet` → `ExGrid`. `ExSheet.Engine` references nothing of ours.
- **What ExSheet asks of ExGrid's core is listed in one place**, ADR-0050. Each item is an opt-in
  declaration, so a plain ExGrid Consumer sees no change.
- **Cell formatting starts at number formats and alignment.** Excel's number format codes
  (`#,##0.00`, `yyyy-mm-dd`) and horizontal alignment are in the first version; a date is a
  number with a date format, so without format codes there are no dates. **Fonts, fills and
  borders are not in it.** A colour a user picks is document data, not a colour passed to the
  component as a C# parameter, so [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)
  does not forbid it. But per-cell styling is paid for on every painted cell
  ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). It waits for a
  `spikes/render-bench` mode that measures it, and an ADR that reads the result.
- **Merged cells are not supported.** A merge is a cell that is not a rectangle of one, and
  Selection is rectangles in index space (ADR-0011).
- **What the first version holds**, beyond this ADR's own decisions:

  | | First version | Later | Why later |
  |---|---|---|---|
  | Inserting and deleting rows and columns | ✓ | | |
  | Pinned Columns (frozen panes, columns) | ✓ | | ExGrid has them |
  | Frozen rows | | ✓ | ExGrid has no pinned rows; a core change |
  | Hiding rows and columns | | ✓ | Selection and copy across hidden cells need rules of their own |
  | Sort (rewriting cells) and AutoFilter (hiding rows) | | ✓ | ExSheet's own commands, above |
  | Find and replace | | ✓ | |
  | Protecting cells | | ✓ | `Editable` is per column ([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)); per cell is a core change |
  | Several Sheets (a workbook) | | ✓ | above |
  | Reading and writing `.xlsx` | | ✓ | a separate package (ADR-0048) |

- **ExSheet is built alongside ExGrid**, before ExGrid's sign-off. A change ExSheet needs in the
  core is made through an ADR, and it has to be right for a plain ExGrid Consumer as well.
