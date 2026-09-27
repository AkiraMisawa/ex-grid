# ExSheet

A general-purpose sheet for Blazor. It holds a **Sheet** — cells addressed `A1` over Excel's
extent of 1,048,576 rows by 16,384 columns — computes its **Values** with
[`ExSheet.Engine`](https://www.nuget.org/packages/ExSheet.Engine), and shows it by rendering one
[`ExGrid`](https://www.nuget.org/packages/ExGrid) as that grid's Consumer: ExSheet holds and
computes, ExGrid paints, selects, navigates and reports.

> **This is a prerelease (`0.x`).** ExSheet is built alongside ExGrid and is not part of its
> release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0`.
- ExGrid's stylesheet and script, as for any ExGrid.

## Showing a Sheet

```razor
@using ExSheet.Components
@using ExSheet.Engine

<ExSheet @bind-Document="_document" ViewportHeight="480" ViewportWidth="900" />

@code {
    private SheetDocument? _document;   // null: an empty Sheet in the current culture
}
```

- **The Sheet Document is yours to keep.** `DocumentChanged` is raised after every change with the
  Sheet's Entries — never its Values — and ExSheet never stores anything. Handing over a different
  document replaces the Sheet and clears the undo stack; handing back the one it raised changes
  nothing.
- **`Culture`** is the declared culture of a Sheet started empty; a document carries its own.
- **`PinnedColumnCount`** freezes leading columns. Every row has the same height, and a
  `RowHeight` at which the full extent would pass the browser's scroll ceiling is refused by name.
- **`ShowRowHeadings`, `ShowColumnHeadings` and `ShowFormulaBar`** hide either Heading or the
  Formula Bar. The cells are still addressed `A1`.
- **`AllowCopyWithHeaders`** puts ExGrid's copy with headers back in the Context Menu. It is off
  by default: a Sheet's column letters are addresses, not headers. Switched on, it copies the
  Values with the column letters as the first row.

What the user gets: typing constants and Formulas, the Formula shown when a cell is edited and in
the Formula Bar, a Name Box that says where the Focus is and takes an address to go to, column and
row selection from the Headings, and Ctrl+arrow stopping where a block of values ends. While a
Formula is typed, function names are completed (Tab accepts), the argument hint shows the
function's arguments, and the arrows and the mouse point at cells to write their References. The
Context Menu inserts and deletes the rows or columns the Selection spans; References keep naming
the same cells, and a deleted target is `#REF!`. `CommandLabel` words the menu, by the ids in
`SheetCommandIds` and the grid's own. A paste may spill from one cell, as in Excel; the pasted block becomes the
Selection, and each field is read as if typed under the Sheet's culture. A copy carries the Values
as the engine writes them: as shown in `text/plain`, unformatted in `text/html`. A copy reaching a
cell still waiting for a Linked Table's data is refused, and the user is told which cell. The fill handle fills as Excel does: Formulas with their
References shifted, a series from two or more numbers, and dates by day. Any other pattern is
refused, and the user is told why.

## Linked Tables

A Formula can read the application's own data as a **Linked Table**, by key or by column, in
Excel's syntax (ADR-0049):

```csharp
await sheet.DeclareLinkedTableAsync("Positions", ["Id", "Book", "PV"]);
await sheet.PushLinkedTableAsync("Positions", positions.Select(p =>
    (IReadOnlyList<Value?>)[Value.FromText(p.Id), Value.FromText(p.Book), Value.FromNumber(p.PV)]));
```

```
=SUM(Positions[PV])
=XLOOKUP("R-4471", Positions[Id], Positions[PV])
```

Until the first snapshot arrives, a Formula reading the table shows `#GETTING_DATA`, and
`IFERROR` does not hide it. Each push replaces the whole table, and only the Formulas reading it
recalculate. The declaration is recorded in the Sheet Document. The rows never are, so a Consumer
pushes again after opening a document.

## Commands and the undo stack

There is one undo stack per ExSheet, and a command the application gives goes onto it in its place
in the order:

```csharp
await sheet.DoAsync(SheetEdit.InsertRows(row: 4));
await sheet.SetNumberFormatAsync(NumberFormat.Parse("#,##0.00"));   // on the selection
await sheet.UndoAsync();
```

A format or an alignment set on a selection of whole columns or whole rows is recorded on the
columns or rows, one entry each, as Excel records it: cell over row over column.

Column widths are part of the Sheet Document, in characters as Excel counts them. Resizing a
column, and a number typed into a column that it widens, are steps on the undo stack like any
other, and raise `DocumentChanged`; an opened document brings its widths with it. A width an entry
widened the column to stays automatic, and a longer entry widens the column again; a width the
user set — a drag or a size to fit — is custom, and entries no longer widen that column.

## More

The decisions behind ExSheet are ADR-0046 to ADR-0051 in
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr).
