using ExGrid.Cells;
using ExGrid.Selection;
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
/// keyboard leaves it, no grid is pointed at, and the edit stands. A Sheet is never pointed at. After
/// a press on a cell, that Sheet's arrow keys point inside the grid pressed: one row up or down in its
/// current order, or to the next column its table has. After a press on a column header, ↓ points at
/// the column's first row, and ← and → at the next column its table has, as a column, which the grid
/// scrolls into view across (ADR-0058, "The keyboard").</para>
///
/// <para>The Scope also draws in its grids what ADR-0057 and ADR-0058 ask of them. While a Formula
/// is edited in a Sheet of the Scope, the Linked Table columns it reads are outlined in the grids
/// that show them, in the colours their References wear: the page wires no <c>OutlinedColumns</c>
/// for a registered grid, and states the correspondence of columns once, when it registers it. And
/// the cell or column a press wrote for is dashed in the Focus outline's colour — a cell found by
/// its row's key, wherever a sort puts it — until Point over what it wrote ends.</para>
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

    // What each Sheet last told of the Linked Table columns its Formula reads, the latest last, so
    // that its outlines are drawn over the others' (ADR-0057). A Sheet whose edit ended has none.
    private readonly List<(IPointingSheet Sheet, LinkedColumnColours Columns)> _linkedColumns = [];

    // The dashes over the cell or column a press wrote for, while what it wrote stands in that
    // Sheet's edit; and those that stood before the last write, which a drag from its press brings
    // back with the text it takes back.
    private Dashed? _dashed;
    private Dashed? _dashedBeforeWrite;

    /// <summary>The dashes a press asked for: on which grid, for which Sheet's Point, and where.</summary>
    private sealed record Dashed(GridMember Grid, IPointingSheet Sheet, PointDashes Where);

    /// <summary>Where a press's dashes stand in its grid: over the named grid column's cell in the row
    /// <paramref name="IsRow"/> answers true for, read as the table's row, or down the column's body
    /// when it is null.</summary>
    private sealed record PointDashes(string Column, Func<IReadOnlyList<Value?>, bool>? IsRow);

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
    /// the one the table holds. Null is a blank. Asked for a pressed cell's row and, while that
    /// cell is dashed, for the rows the grid paints, to find the one with its key: it must be cheap
    /// and must not throw.</param>
    /// <param name="tableColumns">Which of the table's columns each grid column is, by the grid
    /// column's name (<c>GridColumn.Name</c>), for the grid columns named otherwise than their table
    /// column. A grid column with the same name as its table column needs no entry — named exactly as
    /// the Sheets declare the column, so that the column is outlined while a Formula reads it — and a
    /// grid column that is not one of the table's is simply not the table's: a press on it is
    /// refused. Null — the default — for a grid whose columns are all named as the table's. The
    /// Scope outlines each table column a Formula reads over the grid columns this makes it, so the
    /// page states the correspondence here and nowhere else.</param>
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
        grid.DeclarePointedAt(_pointing);
        grid.Outline(_linkedColumns);
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
        PointStateChanged(sheet, PointState.None);
        OutlineLinkedColumns(sheet, LinkedColumnColours.None);
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
            grid.DeclarePointedAt(pointedAt ? _pointing : null);
    }

    /// <summary>
    /// A Sheet of the Scope tells the Linked Table columns the Formula it edits reads, and their
    /// colours, or none once the edit ends (ADR-0057). Each registered grid outlines those of its
    /// table over the grid columns that are them, whether or not the Sheet points: the outlines stay
    /// while the edit is open, as every Reference Outline does.
    /// </summary>
    internal void OutlineLinkedColumns(IPointingSheet sheet, LinkedColumnColours columns)
    {
        var told = _linkedColumns.FindIndex(entry => ReferenceEquals(entry.Sheet, sheet));
        if (told >= 0)
            _linkedColumns.RemoveAt(told);
        if (columns.Count > 0 && _sheets.Contains(sheet))
            _linkedColumns.Add((sheet, columns));
        else if (told < 0)
            return;
        foreach (var grid in _grids)
            grid.Outline(_linkedColumns);
    }

    /// <summary>
    /// A Sheet of the Scope tells where its edit stands with respect to Point. While what a press on
    /// a registered grid wrote stands, that is <see cref="PointState.WrittenFromOutside"/>. Anything
    /// else means Point over it has ended — an operator typed, the caret moved, a press on the
    /// Sheet, a commit or a cancel — and the dashes that press asked for go (ADR-0058, "What is
    /// drawn"). The column outlines stay until the edit ends.
    /// </summary>
    internal void PointStateChanged(IPointingSheet sheet, PointState state)
    {
        if (state == PointState.WrittenFromOutside)
            return;
        if (ReferenceEquals(_dashedBeforeWrite?.Sheet, sheet))
            _dashedBeforeWrite = null;
        if (ReferenceEquals(_dashed?.Sheet, sheet))
            Dash(null);
    }

    /// <summary>Dashes where a press asked, or nowhere; the grid that had them before loses
    /// them.</summary>
    private void Dash(Dashed? dashed)
    {
        if (_dashed is { } shown && !ReferenceEquals(shown.Grid, dashed?.Grid))
            shown.Grid.ShowDashes(null);
        _dashed = dashed;
        dashed?.Grid.ShowDashes(dashed.Where);
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
        if (tookBack)
            Dash(_dashedBeforeWrite);
        _dashedBeforeWrite = null;
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
        PointDashes dashes;
        if (kind == GridPointedPressKind.ColumnHeader)
        {
            (text, dashes) = ColumnReference(table, tableColumn, column);
        }
        else
        {
            if (CellLookup(table, tableColumn, column, row, out var refused) is not { } lookup)
            {
                await RefuseAsync(sheet, refused, table.Name, tableColumn, false);
                return;
            }
            (text, dashes) = lookup;
        }
        var dashedBefore = _dashed;
        if (!await sheet.WritePointedTextAsync(text))
        {
            // The Sheet's edit left Point while the press was on its way.
            await RefuseAsync(sheet, PointingRefusalReason.NotPointing, table.Name, tableColumn, false);
            return;
        }
        _wrote = grid;
        _dashedBeforeWrite = ReferenceEquals(dashedBefore?.Sheet, sheet) ? dashedBefore : null;
        Dash(new Dashed(grid, sheet, dashes));
    }

    /// <summary>
    /// An arrow key in a Sheet of the Scope while what a press on a registered grid wrote stands in its
    /// edit (ADR-0058, "The keyboard"): the cell or the column that press wrote for moves one step
    /// inside that grid, and the text is rewritten for what it reached, replacing what this Point
    /// wrote. From a cell: a row up or down in the grid's current order, or the next column the table
    /// has left or right, passing over the grid's columns the table does not have. From a column (Part
    /// B of the ninth Windows run, Q52): down to the column's first row, or the next column the table
    /// has left or right, as a column; up is an edge. The dashes move with it, and the grid scrolls a
    /// cell reached into view, and a column reached into view across only (2026-10-01). At an edge
    /// nothing moves. A row that has not arrived, Shift and an arrow, and the Primary Modifier and an
    /// arrow write nothing, leave the text as it was, and the reason is told.
    /// </summary>
    internal async Task PointArrowAsync(IPointingSheet sheet, GridPointArrow arrow)
    {
        // The dashes name the cell or the column this Sheet's Point wrote for: without them, nothing a
        // press on a registered grid wrote stands there.
        if (!ReferenceEquals(_pointing, sheet) || _dashed is not { } dashed || !ReferenceEquals(dashed.Sheet, sheet))
            return;
        var grid = dashed.Grid;
        if (arrow.ToEdge)
        {
            await sheet.TellPointingRefusedAsync(Refusal(PointingRefusalReason.DataEdge, grid.Table, null, false));
            return;
        }
        if (arrow.Extends)
        {
            await sheet.TellPointingRefusedAsync(new PointingRefusal(PointingRefusalReason.SeveralCells, SheetWords.PointingExtendedByKey(grid.Table)));
            return;
        }
        if (FindTable(sheet, grid.Table) is not { } table)
        {
            await RefuseAsync(sheet, PointingRefusalReason.TableNotDeclared, grid.Table, null, false);
            return;
        }
        bool IsTableColumn(string gridColumn) => FindColumn(table, grid.TableColumnOf(gridColumn)) is not null;
        var fromColumn = dashed.Where.IsRow is null;
        var step = fromColumn
            ? await grid.StepFromColumnAsync(dashed.Where.Column, arrow.Direction, IsTableColumn)
            : await grid.StepAsync(dashed.Where, arrow.Direction, IsTableColumn);
        switch (step.Kind)
        {
            case GridPointedStepKind.Edge:
                return;
            case GridPointedStepKind.RowNotArrived:
                await RefuseAsync(sheet, PointingRefusalReason.RowNotArrived, table.Name, null, false);
                return;
            case GridPointedStepKind.NotHeld:
                // From a column, the column is named: it is what the grid no longer shows.
                var notShown = fromColumn ? FindColumn(table, grid.TableColumnOf(dashed.Where.Column)) ?? dashed.Where.Column : null;
                await RefuseAsync(sheet, PointingRefusalReason.CellNotHeld, table.Name, notShown, false);
                return;
        }
        var column = step.Column!;
        if (FindColumn(table, grid.TableColumnOf(column)) is not { } tableColumn)
        {
            await RefuseAsync(sheet, PointingRefusalReason.ColumnNotInTable, table.Name, column, false);
            return;
        }
        string text;
        PointDashes dashes;
        if (step.Kind == GridPointedStepKind.Column)
        {
            (text, dashes) = ColumnReference(table, tableColumn, column);
        }
        else
        {
            if (CellLookup(table, tableColumn, column, step.Row, out var refused) is not { } lookup)
            {
                await RefuseAsync(sheet, refused, table.Name, tableColumn, false);
                return;
            }
            (text, dashes) = lookup;
        }
        if (!await sheet.WritePointedTextAsync(text))
        {
            await RefuseAsync(sheet, PointingRefusalReason.NotPointing, table.Name, tableColumn, false);
            return;
        }
        // A gesture of its own: a drag from the press that began this no longer takes anything back.
        _wrote = null;
        _dashedBeforeWrite = null;
        Dash(new Dashed(grid, sheet, dashes));
        await grid.RevealAsync(dashes);
    }

    /// <summary>What a column of a registered grid writes, <c>T[&lt;column&gt;]</c>, as a press on its
    /// header or an arrow from another column reaches it, and the dashes down its body.</summary>
    private static (string Text, PointDashes Dashes) ColumnReference(LinkedTable table, string tableColumn, string gridColumn)
        => (FormulaEntry.StructuredReferenceText(table.Name, tableColumn), new PointDashes(gridColumn, IsRow: null));

    /// <summary>
    /// What a cell of a registered grid writes, <c>XLOOKUP(&lt;key&gt;, T[&lt;key column&gt;],
    /// T[&lt;column&gt;])</c>, and the dashes that find it by its row's key; or null, with the reason
    /// nothing is written: a row that has not arrived, a table declared without a key, a blank key or
    /// an Error Value as the key.
    /// </summary>
    private static (string Text, PointDashes Dashes)? CellLookup(
        LinkedTable table, string tableColumn, string gridColumn, Func<IReadOnlyList<Value?>>? row, out PointingRefusalReason refused)
    {
        refused = default;
        if (row is null)
        {
            refused = PointingRefusalReason.RowNotArrived;
            return null;
        }
        if (table.Key is not { } keyColumn)
        {
            refused = PointingRefusalReason.NoKey;
            return null;
        }
        var values = row();
        if (values is null || values.Count != table.Columns.Count)
        {
            throw new InvalidOperationException(
                $"The row a grid registered for '{table.Name}' gave has {values?.Count ?? 0} Values, and the table declares {table.Columns.Count} columns: " +
                "RegisterGrid's tableRow gives one Value per declared column, in the declared order, as a push does (ADR-0058).");
        }
        var keyIndex = IndexOf(table, keyColumn);
        if (values[keyIndex] is not { } key)
        {
            refused = PointingRefusalReason.BlankKey;
            return null;
        }
        if (key.IsError)
        {
            refused = PointingRefusalReason.KeyIsAnError;
            return null;
        }
        var width = table.Columns.Count;
        return (FormulaEntry.LookupText(table.Name, keyColumn, key, tableColumn),
            new PointDashes(gridColumn, candidate => candidate.Count == width && FormulaEntry.LookupFinds(key, candidate[keyIndex])));
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

        /// <summary>The grid columns that are a table column, as a press finds them: each given it as
        /// its entry, and the one of its own name unless that one is given another.</summary>
        private IEnumerable<string> GridColumnsOf(string tableColumn)
        {
            foreach (var (gridColumn, entry) in tableColumns)
            {
                if (string.Equals(entry, tableColumn, StringComparison.OrdinalIgnoreCase))
                    yield return gridColumn;
            }
            if (!tableColumns.ContainsKey(tableColumn))
                yield return tableColumn;
        }

        /// <summary>
        /// Outlines the columns of this grid's table that the Sheets told, each in its colour, over
        /// the grid columns that are it (ADR-0057, ADR-0058): a new list only when it differs from the
        /// one the grid holds, since the list instance is the declaration's change signal.
        /// </summary>
        public void Outline(IReadOnlyList<(IPointingSheet Sheet, LinkedColumnColours Columns)> told)
        {
            List<OutlinedColumn>? outlined = null;
            foreach (var (_, columns) in told)
            {
                foreach (var column in columns)
                {
                    if (!string.Equals(column.Column.Table, Table, StringComparison.OrdinalIgnoreCase))
                        continue;
                    foreach (var gridColumn in GridColumnsOf(column.Column.Column))
                        (outlined ??= []).Add(new OutlinedColumn(gridColumn, column.Colour));
                }
            }
            var current = OutlinedColumns;
            if (outlined is null ? current is null : current is not null && current.SequenceEqual(outlined))
                return;
            OutlinedColumns = outlined;
        }

        /// <summary>Declares the grid pointed at from the Sheet that points, or, given none, not
        /// pointed at.</summary>
        public abstract void DeclarePointedAt(IPointingSheet? pointing);

        /// <summary>The columns the grid's declaration outlines.</summary>
        protected abstract IReadOnlyList<OutlinedColumn>? OutlinedColumns { get; set; }

        /// <summary>Draws the dashes a press asked for, or takes them away.</summary>
        public abstract void ShowDashes(PointDashes? dashes);

        /// <summary>Asks the grid for the cell one step from the dashed one (DC-55): the row it
        /// reached, read as the table's row, and the grid column.</summary>
        public abstract Task<Step> StepAsync(PointDashes from, GridDirection direction, Func<string, bool> isColumn);

        /// <summary>Asks the grid what one step from the dashed column reaches (DC-55): its first row,
        /// read as the table's row, or another grid column, as a column.</summary>
        public abstract Task<Step> StepFromColumnAsync(string column, GridDirection direction, Func<string, bool> isColumn);

        /// <summary>Asks the grid to scroll the dashed cell into view, or the dashed column into view
        /// across only (DC-55).</summary>
        public abstract Task RevealAsync(PointDashes dashes);
    }

    /// <summary>Where a step from a cell or a column of a registered grid landed, whatever its row
    /// type.</summary>
    private sealed record Step(GridPointedStepKind Kind, Func<IReadOnlyList<Value?>>? Row, string? Column);

    /// <summary>A registered grid of rows of <typeparamref name="TRow"/>: its declaration hands each
    /// press to the Scope, with the pressed row read as the table's row.</summary>
    private sealed class GridMember<TRow> : GridMember
        where TRow : class
    {
        private readonly Func<TRow, IReadOnlyList<Value?>> _tableRow;

        public GridMember(PointingScope scope, string table, Func<TRow, IReadOnlyList<Value?>> tableRow, IReadOnlyDictionary<string, string> tableColumns)
            : base(table, tableColumns)
        {
            _tableRow = tableRow;
            Declaration = new GridPointedAt<TRow>(press => scope.OnPressAsync(
                this,
                press.Kind,
                press.Row is { } row ? () => tableRow(row) : null,
                press.Column,
                press.Dragged));
        }

        /// <summary>The declaration passed to the grid.</summary>
        public GridPointedAt<TRow> Declaration { get; }

        public override void DeclarePointedAt(IPointingSheet? pointing)
        {
            // The Sheet's root is named first, so the render that paints the grid pointed at names
            // it too: a press on the grid then keeps its place among the Sheet's keys (ADR-0058, "On
            // a circuit").
            if (pointing is not null)
                Declaration.PointingRootId = pointing.RootId;
            Declaration.IsPointedAt = pointing is not null;
        }

        protected override IReadOnlyList<OutlinedColumn>? OutlinedColumns
        {
            get => Declaration.OutlinedColumns;
            set => Declaration.OutlinedColumns = value;
        }

        // A cell is found by its row's key in the rows the grid paints, so the dashes follow the row
        // through a sort, and through a Window of new instances, and are drawn nowhere while it is
        // not painted (DC-53).
        public override void ShowDashes(PointDashes? dashes) => Declaration.Dashes = dashes switch
        {
            null => null,
            { IsRow: { } isRow } => GridPointDashes<TRow>.OverCell(row => isRow(_tableRow(row)), dashes.Column),
            _ => GridPointDashes<TRow>.OverColumn(dashes.Column),
        };

        // The cell stepped from, and the one revealed, are found by their row's key, as the dashes are.
        public override async Task<Step> StepAsync(PointDashes from, GridDirection direction, Func<string, bool> isColumn)
        {
            var isRow = from.IsRow!;
            return Read(await Declaration.StepAsync(row => isRow(_tableRow(row)), from.Column, direction, isColumn));
        }

        public override async Task<Step> StepFromColumnAsync(string column, GridDirection direction, Func<string, bool> isColumn)
            => Read(await Declaration.StepFromColumnAsync(column, direction, isColumn));

        /// <summary>A step the grid answered, with the row it reached read as the table's row.</summary>
        private Step Read(GridPointedStep<TRow> step)
            => new(step.Kind, step.Row is { } reached ? () => _tableRow(reached) : null, step.Column);

        public override Task RevealAsync(PointDashes dashes) => dashes.IsRow is { } isRow
            ? Declaration.RevealAsync(row => isRow(_tableRow(row)), dashes.Column)
            : Declaration.RevealColumnAsync(dashes.Column);
    }
}
