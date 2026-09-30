using ExGrid.Cells;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// A Pointing Scope (ADR-0058): the Sheets and the grids a Consumer groups so that a Formula edited
/// in one of the Sheets can point at the grids' cells. Pointing at a grid of the Scope writes what
/// reads the pressed cell by its row's key, <c>XLOOKUP("R-4471", Positions[Id], Positions[PV])</c>,
/// or the pressed column's structured reference, <c>Positions[PV]</c> — never a position, so the
/// Formula reads the same row after the grid is sorted (ADR-0049, rule 2).
///
/// <para>The Consumer makes one per group, as the page is made, and passes it to each ExSheet of the
/// group (<c>ExSheet.PointingScope</c>) and, through <see cref="RegisterGrid"/>, to each ExGrid that
/// shows a Linked Table (<c>ExGrid.PointedAt</c>). Nothing on a page is joined unless it is put in
/// the same Scope: a grid in none behaves as it always does, and two Scopes divide a page.</para>
///
/// <para>A registered grid is pointed at while a Sheet of the Scope holds the keyboard, has an edit
/// open, and is in Point — its caret stands where a Reference can go, or what Point wrote there still
/// stands — and while the grid holds no open edit of its own. Then a press on its rows or its column
/// headers moves neither DOM focus nor its Selection, and is written into that Sheet's edit where
/// Point writes, or refused with a reason the Sheet tells its Consumer
/// (<c>ExSheet.OnPointingRefused</c>). Only the Sheet that holds the keyboard points; when the
/// keyboard leaves it, no grid is pointed at, and the edit stands. A Sheet is never pointed at.</para>
///
/// <para>The key column is the table's own: the Scope reads it, and the table's column names, from
/// the declaration of the Sheet that points (<c>DeclareLinkedTableAsync</c>). A Scope belongs to one
/// page, on one renderer: it is not shared between users.</para>
/// </summary>
public sealed class PointingScope
{
    private readonly List<IPointingSheet> _sheets = [];
    private readonly List<GridMember> _grids = [];

    // The Sheet that points now, and the one that pointed last, which hears of a press that
    // arrived after it stopped.
    private IPointingSheet? _pointing;
    private IPointingSheet? _pointedLast;

    // The grid whose press wrote what the pointing Sheet's Point holds: a drag from that press takes
    // it back. Any other press forgets it.
    private GridMember? _wrote;

    /// <summary>
    /// Registers a grid that shows a Linked Table, and gives back the declaration to pass it as its
    /// <c>PointedAt</c> parameter. The Scope declares the grid pointed at while a Sheet of the Scope
    /// points, and answers each press the grid hands over. Register a grid once, as the page is made.
    /// </summary>
    /// <typeparam name="TRow">The grid's row type.</typeparam>
    /// <param name="table">The name of the Linked Table the grid shows, as the Sheets declare it;
    /// matched without regard to case, as a Formula names it.</param>
    /// <param name="tableRow">A grid row as the table's row: one Value per declared column, in the
    /// order the columns are declared — what the Consumer pushes for the row
    /// (<c>PushLinkedTableAsync</c>). A pressed cell's key is read from it, so the key written is
    /// the one the table holds. Null is a blank. Asked only for a pressed cell's row.</param>
    /// <param name="tableColumns">Which of the table's columns each grid column is, by the grid
    /// column's name (<c>GridColumn.Name</c>), for the grid columns named otherwise than their table
    /// column. A grid column with the same name as its table column needs no entry, and a grid column
    /// that is not one of the table's is simply not the table's: a press on it is refused. Null — the
    /// default — for a grid whose columns are all named as the table's.</param>
    /// <returns>The declaration to pass to the grid's <c>PointedAt</c> parameter.</returns>
    /// <exception cref="ArgumentException">The table's name is empty, or an entry of
    /// <paramref name="tableColumns"/> names no column.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public GridPointedAt<TRow> RegisterGrid<TRow>(
        string table, Func<TRow, IReadOnlyList<Value?>> tableRow, IReadOnlyDictionary<string, string>? tableColumns = null)
        where TRow : class
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(tableRow);
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tableColumns is not null)
        {
            foreach (var (gridColumn, tableColumn) in tableColumns)
            {
                if (string.IsNullOrEmpty(gridColumn) || string.IsNullOrEmpty(tableColumn))
                    throw new ArgumentException("A grid column and the table column it is are both named (ADR-0058).", nameof(tableColumns));
                columns[gridColumn] = tableColumn;
            }
        }
        var grid = new GridMember<TRow>(this, table, tableRow, columns);
        _grids.Add(grid);
        grid.DeclarePointedAt(_pointing is not null);
        return grid.Declaration;
    }

    /// <summary>A Sheet given this Scope joins it.</summary>
    internal void Join(IPointingSheet sheet)
    {
        if (!_sheets.Contains(sheet))
            _sheets.Add(sheet);
    }

    /// <summary>A Sheet no longer given this Scope, or disposed, leaves it; if it pointed, no grid is
    /// pointed at.</summary>
    internal void Leave(IPointingSheet sheet)
    {
        Points(sheet, false);
        _sheets.Remove(sheet);
        if (ReferenceEquals(_pointedLast, sheet))
            _pointedLast = null;
    }

    /// <summary>
    /// A Sheet of the Scope says whether it points: it holds the keyboard, has an edit open, and is in
    /// Point. The grids are pointed at while one does. A Sheet that stops pointing after another
    /// started — the keyboard went from one to the other — changes nothing.
    /// </summary>
    internal void Points(IPointingSheet sheet, bool points)
    {
        if (points)
        {
            if (!_sheets.Contains(sheet))
                return;
            if (!ReferenceEquals(_pointing, sheet))
                _wrote = null;
            _pointing = sheet;
            _pointedLast = sheet;
            DeclarePointedAt(true);
        }
        else if (ReferenceEquals(_pointing, sheet))
        {
            _pointing = null;
            _wrote = null;
            DeclarePointedAt(false);
        }
    }

    private void DeclarePointedAt(bool pointedAt)
    {
        foreach (var grid in _grids)
            grid.DeclarePointedAt(pointedAt);
    }

    /// <summary>
    /// A press a registered grid handed over (ADR-0058): written into the pointing Sheet's edit where
    /// Point writes, or refused with the reason, and a drag takes back what its press wrote.
    /// </summary>
    private async Task OnPressAsync(GridMember grid, GridPointedPressKind kind, Func<IReadOnlyList<Value?>>? row, string? column, bool dragged)
    {
        // The press a drag began with was handed over first, and may have written: the drag shows
        // the user meant a range, so the text goes back to what it was before the press. Any other
        // press is a gesture of its own, and forgets which press wrote last.
        var tookBack = dragged && ReferenceEquals(_wrote, grid) && _pointing is { } writer
            && await writer.TakeBackPointedTextAsync();
        _wrote = null;
        if (_pointing is not { } sheet)
        {
            // Handed over by a grid still painted pointed at, after the Sheet stopped pointing.
            if (_pointedLast is { } last)
                await last.TellPointingRefusedAsync(Refusal(PointingRefusalReason.NotPointing, grid.Table, null, false));
            return;
        }
        switch (kind)
        {
            case GridPointedPressKind.SeveralCells:
                await RefuseAsync(sheet, PointingRefusalReason.SeveralCells, grid.Table, null, tookBack);
                return;
            case GridPointedPressKind.SeveralColumns:
                await RefuseAsync(sheet, PointingRefusalReason.SeveralColumns, grid.Table, null, tookBack);
                return;
            case GridPointedPressKind.HeaderGroup:
                await RefuseAsync(sheet, PointingRefusalReason.HeaderGroup, grid.Table, null, false);
                return;
        }
        if (FindTable(sheet, grid.Table) is not { } table)
        {
            await RefuseAsync(sheet, PointingRefusalReason.TableNotDeclared, grid.Table, null, false);
            return;
        }
        if (column is null || FindColumn(table, grid.TableColumnOf(column)) is not { } tableColumn)
        {
            await RefuseAsync(sheet, PointingRefusalReason.ColumnNotInTable, table.Name, column, false);
            return;
        }
        string text;
        if (kind == GridPointedPressKind.ColumnHeader)
        {
            text = FormulaEntry.StructuredReferenceText(table.Name, tableColumn);
        }
        else
        {
            if (row is null)
            {
                await RefuseAsync(sheet, PointingRefusalReason.RowNotArrived, table.Name, tableColumn, false);
                return;
            }
            if (table.Key is not { } keyColumn)
            {
                await RefuseAsync(sheet, PointingRefusalReason.NoKey, table.Name, tableColumn, false);
                return;
            }
            var values = row();
            if (values is null || values.Count != table.Columns.Count)
            {
                throw new InvalidOperationException(
                    $"The row a grid registered for '{table.Name}' gave has {values?.Count ?? 0} Values, and the table declares {table.Columns.Count} columns: " +
                    "RegisterGrid's tableRow gives one Value per declared column, in the declared order, as a push does (ADR-0058).");
            }
            var key = values[IndexOf(table, keyColumn)];
            if (key is not { } value)
            {
                await RefuseAsync(sheet, PointingRefusalReason.BlankKey, table.Name, tableColumn, false);
                return;
            }
            if (value.IsError)
            {
                await RefuseAsync(sheet, PointingRefusalReason.KeyIsAnError, table.Name, tableColumn, false);
                return;
            }
            text = FormulaEntry.LookupText(table.Name, keyColumn, value, tableColumn);
        }
        if (!await sheet.WritePointedTextAsync(text))
        {
            // The Sheet's edit left Point while the press was on its way.
            await RefuseAsync(sheet, PointingRefusalReason.NotPointing, table.Name, tableColumn, false);
            return;
        }
        _wrote = grid;
    }

    private static Task RefuseAsync(IPointingSheet sheet, PointingRefusalReason reason, string table, string? column, bool tookBack)
        => sheet.TellPointingRefusedAsync(Refusal(reason, table, column, tookBack));

    private static PointingRefusal Refusal(PointingRefusalReason reason, string table, string? column, bool tookBack)
        => new(reason, SheetWords.PointingRefused(reason, table, column, tookBack));

    /// <summary>The pointing Sheet's declaration of the table, found as a Formula finds a table:
    /// without regard to case.</summary>
    private static LinkedTable? FindTable(IPointingSheet sheet, string name)
    {
        foreach (var table in sheet.LinkedTables)
        {
            if (string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase))
                return table;
        }
        return null;
    }

    /// <summary>The table's column of that name, as declared, found without regard to case; null for
    /// a column the table does not have.</summary>
    private static string? FindColumn(LinkedTable table, string name)
    {
        foreach (var column in table.Columns)
        {
            if (string.Equals(column, name, StringComparison.OrdinalIgnoreCase))
                return column;
        }
        return null;
    }

    private static int IndexOf(LinkedTable table, string column)
    {
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (string.Equals(table.Columns[i], column, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new InvalidOperationException($"The key '{column}' is not one of the columns of '{table.Name}'.");
    }

    /// <summary>A grid registered in the Scope: the table it shows and which of its columns are the
    /// table's, whatever its row type.</summary>
    private abstract class GridMember(string table, IReadOnlyDictionary<string, string> tableColumns)
    {
        /// <summary>The Linked Table the grid shows, as it was registered.</summary>
        public string Table { get; } = table;

        /// <summary>The name of the table column a grid column is: its entry, or its own name.</summary>
        public string TableColumnOf(string gridColumn)
            => tableColumns.TryGetValue(gridColumn, out var tableColumn) ? tableColumn : gridColumn;

        /// <summary>Declares the grid pointed at, or not.</summary>
        public abstract void DeclarePointedAt(bool pointedAt);
    }

    /// <summary>A registered grid of rows of <typeparamref name="TRow"/>: its declaration hands each
    /// press to the Scope, with the pressed row read as the table's row.</summary>
    private sealed class GridMember<TRow> : GridMember
        where TRow : class
    {
        public GridMember(PointingScope scope, string table, Func<TRow, IReadOnlyList<Value?>> tableRow, IReadOnlyDictionary<string, string> tableColumns)
            : base(table, tableColumns)
        {
            Declaration = new GridPointedAt<TRow>(press => scope.OnPressAsync(
                this,
                press.Kind,
                press.Row is { } row ? () => tableRow(row) : null,
                press.Column,
                press.Dragged));
        }

        /// <summary>The declaration passed to the grid.</summary>
        public GridPointedAt<TRow> Declaration { get; }

        public override void DeclarePointedAt(bool pointedAt) => Declaration.IsPointedAt = pointedAt;
    }
}
