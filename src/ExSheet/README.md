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
  document replaces the Sheet and clears the undo stack, and an edit open at that moment is
  discarded, and said, rather than entered into the new document; handing back the one it raised
  changes nothing.
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
Formula is typed, function and Linked Table names are completed (Tab accepts), a table's columns
are listed after `Table[`, an argument that takes one of a fixed list — `XLOOKUP`'s `match_mode` and
`search_mode` — lists its values with Excel's texts and Tab writes the value, the argument hint shows the
function's arguments, and the arrows and the mouse point at cells to write their References. The
Context Menu inserts and deletes the rows or columns the Selection spans; References keep naming
the same cells, and a deleted target is `#REF!`. `CommandLabel` words the menu, by the ids in
`SheetCommandIds` and the grid's own. A paste may spill from one cell, as in Excel; the pasted block becomes the
Selection, and each field is read as if typed under the Sheet's culture. A copy carries the Values
as the engine writes them: as shown in `text/plain`, unformatted in `text/html`. A copy reaching a
cell still waiting for a Linked Table's data is refused, and the user is told which cell. The fill handle fills as Excel does: Formulas with their
References shifted, a series from two or more numbers, and dates by day. Any other pattern is
refused, and the user is told why. Ctrl+D and Ctrl+R copy the range's first row or column over the
rest, Formulas with their References shifted, and never continue a series. Delete clears the
Selection's contents and keeps its formats, as one undo step. Ctrl+F finds text as it is shown, in
every row of the Sheet.

## Linked Tables

A Formula can read the application's own data as a **Linked Table**, by key or by column, in
Excel's syntax (ADR-0049):

```csharp
await sheet.DeclareLinkedTableAsync("Positions", ["Id", "Book", "PV"], key: "Id");
await sheet.PushLinkedTableAsync("Positions", positions.Select(p =>
    (IReadOnlyList<Value?>)[Value.FromText(p.Id), Value.FromText(p.Book), Value.FromNumber(p.PV)]));
```

```
=SUM(Positions[PV])
=XLOOKUP("R-4471", Positions[Id], Positions[PV])
```

Until the first snapshot arrives, a Formula reading the table shows `#GETTING_DATA`, and
`IFERROR` does not hide it. Each push replaces the whole table, and only the Formulas reading it
recalculate. The declaration, its key included, is recorded in the Sheet Document. The rows never
are, so a Consumer pushes again after opening a document.

The key is optional, and names one of the columns. A row is read by it, so no key may appear twice
in a snapshot, compared as `XLOOKUP` compares (`r-4471` and `R-4471` are one key; blank keys are not
compared). A push in which a key repeats throws `RepeatedKeyException`, naming the table, the key
column and the key, and the table goes back to waiting: its readers show `#GETTING_DATA`, never the
last snapshot's numbers. A table whose rows are told apart by several columns needs a column that
joins them (`ACME|5Y`) as its key.

While a Formula is edited, each Reference in it wears a colour, and the cells it names on the Sheet
are outlined in that colour (ADR-0057). A table's column is not on the Sheet, so ExSheet tells you
which declared columns the Formula reads and in which colour, and the grid that shows the table
outlines them. A grid registered in a Pointing Scope (below) is outlined by the Scope, with nothing
more to write. For a grid in no Scope, you pass the list on yourself. It is empty when the edit ends:

```razor
@using ExGrid.Cells
@using ExSheet

<ExSheet @bind-Document="_document" OnLinkedColumnColoursChanged="Outline" />
<ExGrid TRow="Position" Window="_positions" Columns="_columns" OutlinedColumns="_outlined" />

@code {
    private IReadOnlyList<OutlinedColumn> _outlined = [];

    // Columns named as the table's are declared: a column the Sheet names is the grid's own.
    private void Outline(LinkedColumnColours columns) =>
        _outlined = [.. columns.Where(c => c.Column.Table == "Positions")
                               .Select(c => new OutlinedColumn(c.Column.Column, c.Colour))];
}
```

Only you can say that the grid shows the rows the Formula reads: a grid filtered to some of them
outlines the rows it shows.

### Pointing at the grids that show them

While a Formula is typed, a press on a cell of the Sheet writes its address. Put the Sheet and the
grids that show your Linked Tables in a **Pointing Scope**, and a press on one of those grids writes
what reads the pressed cell by its row's key, or the pressed column (ADR-0058):

```
=XLOOKUP("R-4471", Positions[Id], Positions[PV])     a cell of the PV column, in the row whose Id is R-4471
=SUM(Positions[PV])                                  the PV column's header
```

No address is written, so the Formula reads the same row after the grid is sorted. You register each
grid with the table it shows and how one of its rows is the table's row — the same Values you push —
and pass the declaration the Scope gives back to the grid:

```razor
@using ExGrid.Cells
@using ExSheet

<ExSheet @bind-Document="_document" PointingScope="_scope" OnPointingRefused="Refused" />
<ExGrid TRow="Position" Window="_positions" Columns="_columns" PointedAt="_pointedAt" />
<p>@_why</p>

@code {
    private readonly PointingScope _scope = new();
    private GridPointedAt<Position>? _pointedAt;
    private string _why = "";

    protected override void OnInitialized() =>
        _pointedAt = _scope.RegisterGrid<Position>("Positions", Row);

    // One Value per declared column, in the declared order: what you push for the row.
    private static IReadOnlyList<Value?> Row(Position p) =>
        [Value.FromText(p.Id), Value.FromText(p.Book), Value.FromNumber(p.PV)];

    private void Refused(PointingRefusal refusal) => _why = refusal.Message;
}
```

The key column is the one the table is declared with (`key: "Id"`), and the names written are the
table's, never the grid's headers. A grid column named otherwise than its table column is named in a
third argument, `new Dictionary<string, string> { ["Present value"] = "PV" }`; a grid column the
table does not have needs nothing. The grid is pointed at only while a Sheet of the Scope holds the
keyboard, has an edit open, and its caret stands where a Reference can go; a press on it then keeps
neither the keyboard nor a Selection. When the keyboard leaves the Sheet, the grid is an ordinary
grid again, and the edit stands. A grid with an edit of its own open is not pointed at.

A press that stands for more than one cell or column, a Header Group, a column the table does not
have, a table declared without a key, a blank key or a row not yet arrived writes nothing. ExSheet
tells you why through `OnPointingRefused`; show `refusal.Message` where your page tells its user
things. A drag that reaches another cell takes back what its press wrote. After a press on a grid,
the Name Box is empty and F4 changes nothing. Several Sheets can share a Scope, and each points while
it holds the keyboard; two Scopes on one page keep their Sheets and grids apart.

After a press on a cell of a grid, the arrow keys point inside that grid, as Excel's move inside
another workbook once it points there: ↑ and ↓ one row further in the order the grid shows, ← and →
to the next column the table has, passing over the grid's own columns. Each rewrites what the press
wrote, and the grid scrolls to keep the cell in view. At the grid's first or last row, or its last
table column, nothing moves. Shift and an arrow, which would point at a range, Ctrl and an arrow,
which would go to the edge of the data the grid does not hold, and a row not yet arrived write
nothing, and `OnPointingRefused` says why. After a press on a column's header, ↓ points at the
column's first row in the order the grid shows, ← and → at the next column the table has, as a
column (`Positions[Id]`), and ↑ moves nothing.

The Scope draws in its grids too. While a Formula is edited in one of its Sheets, the table columns
the Formula reads are outlined in the grids registered for that table, in the colours their
References wear, over the grid columns the registration makes them: pass no `OutlinedColumns` to a
registered grid, since the correspondence is stated once, when you register it. A grid column that
is its table column by name is named exactly as the table declares it. The cell a press wrote for
is dashed, found by its row's key wherever a sort puts it and drawn nowhere while that row is not
painted; a pressed header dashes the column. The dashes go once something is typed after what the
press wrote, the caret moves, or the edit ends; the column outlines stay until the edit ends. In the
editor, the written `XLOOKUP(...)` is shown selected as a whole, unless it follows the `=` directly.

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

While an edit is open — a cell or the Formula Bar typed in, and not yet committed or cancelled —
these commands, `RedoAsync` and `SetAlignmentAsync` among them, are refused with
`SheetRefusalReason.EditIsOpen` and change nothing, as Excel greys out its ribbon while a cell is
edited: a row inserted above the cell would otherwise carry the typing into another row.
`IsEditing` says whether an edit is open, and `EditingChanged` is raised when that changes, so the
application can grey out its own buttons:

```razor
<ExSheet @ref="_sheet" EditingChanged="open => _editing = open" />
<button disabled="@_editing" @onclick="InsertRowAsync">Insert a row</button>
```

A Linked Table's declaration and snapshots are data arriving, not commands, and are taken while an
edit is open.

Column widths are part of the Sheet Document, in characters as Excel counts them. Resizing a
column, and a number typed into a column that it widens, are steps on the undo stack like any
other, and raise `DocumentChanged`; an opened document brings its widths with it. A width an entry
widened the column to stays automatic, and a longer entry widens the column again; a width the
user set — a drag or a size to fit — is custom, and entries no longer widen that column.

## More

The decisions behind ExSheet are ADR-0046 to ADR-0051 in
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr).
