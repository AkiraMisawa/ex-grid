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

What the user gets: typing constants and Formulas, the Formula shown when a cell is edited and in
the Formula Bar, a Name Box that says where the Focus is and takes an address to go to, column and
row selection from the Headings, and Ctrl+arrow stopping where a block of values ends. While a
Formula is typed, function names are completed (Tab accepts), the argument hint shows the
function's arguments, and the arrows and the mouse point at cells to write their References. The
Context Menu inserts and deletes the rows or columns the Selection spans; References keep naming
the same cells, and a deleted target is `#REF!`. `CommandLabel` words the menu, by the ids in
`SheetCommandIds` and the grid's own.

## Commands and the undo stack

There is one undo stack per ExSheet, and a command the application gives goes onto it in its place
in the order:

```csharp
await sheet.DoAsync(SheetEdit.InsertRows(row: 4));
await sheet.SetNumberFormatAsync(NumberFormat.Parse("#,##0.00"));   // on the selection
await sheet.UndoAsync();
```

## More

The decisions behind ExSheet are ADR-0046 to ADR-0051 in
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr).
