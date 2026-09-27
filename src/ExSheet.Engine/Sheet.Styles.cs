using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// The number format and alignment set on a whole row or a whole column (ADR-0047):
/// <see langword="null"/> where that level sets nothing.
/// </summary>
internal readonly record struct AxisStyle(NumberFormat? Format, HorizontalAlignment? Alignment)
{
    public bool IsEmpty => Format is null && Alignment is null;
}

/// <summary>What a formatting command sets: each property that is not <see langword="null"/>.</summary>
internal readonly record struct StylePatch(NumberFormat? Format, HorizontalAlignment? Alignment);

public sealed partial class Sheet
{
    /// <summary>The formats set on whole rows, by row; a row absent sets nothing (ADR-0047).</summary>
    private Dictionary<int, AxisStyle> _rowStyles = [];

    /// <summary>The formats set on whole columns, by column; a column absent sets nothing (ADR-0047).</summary>
    private Dictionary<int, AxisStyle> _columnStyles = [];

    /// <summary>
    /// The cell's number format, as it takes effect: the cell's own, else its row's, else its
    /// column's, else <see cref="NumberFormat.General"/> — cell over row over column, as in Excel
    /// (ADR-0047).
    /// </summary>
    public NumberFormat GetFormat(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Format : null) ?? InheritedFormat(address);

    /// <summary>The cell's horizontal alignment setting, as it takes effect: cell over row over column (ADR-0047).</summary>
    public HorizontalAlignment GetAlignment(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Alignment : null) ?? InheritedAlignment(address);

    /// <summary>The number format set on the whole row, or <see langword="null"/> when none is (ADR-0047).</summary>
    public NumberFormat? GetRowFormat(int row) => _rowStyles.TryGetValue(CheckRow(row), out var style) ? style.Format : null;

    /// <summary>The number format set on the whole column, or <see langword="null"/> when none is (ADR-0047).</summary>
    public NumberFormat? GetColumnFormat(int column) => _columnStyles.TryGetValue(CheckColumn(column), out var style) ? style.Format : null;

    /// <summary>The alignment set on the whole row, or <see langword="null"/> when none is (ADR-0047).</summary>
    public HorizontalAlignment? GetRowAlignment(int row) => _rowStyles.TryGetValue(CheckRow(row), out var style) ? style.Alignment : null;

    /// <summary>The alignment set on the whole column, or <see langword="null"/> when none is (ADR-0047).</summary>
    public HorizontalAlignment? GetColumnAlignment(int column) => _columnStyles.TryGetValue(CheckColumn(column), out var style) ? style.Alignment : null;

    /// <summary>Sets the number format of cells; <see langword="null"/> is <see cref="NumberFormat.General"/>. No Value changes.</summary>
    public SheetChange SetFormat(IEnumerable<CellAddress> addresses, NumberFormat? format)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return StyleCells(addresses, new StylePatch(format ?? NumberFormat.General, null), null).Change;
    }

    /// <summary>Sets one cell's number format.</summary>
    public SheetChange SetFormat(CellAddress address, NumberFormat? format) => SetFormat([address], format);

    /// <summary>
    /// Sets the number format of a range; <see langword="null"/> is <see cref="NumberFormat.General"/>.
    /// Whole columns (<see cref="CellRange.IsWholeColumns"/>) record one entry per column, and
    /// whole rows one per row, never one per cell (ADR-0047); the cells inside that set their own
    /// format take the new one, as Excel's do. Any other range sets each of its cells. No Value
    /// changes.
    /// </summary>
    public SheetChange SetFormat(CellRange range, NumberFormat? format) =>
        ApplyStyle(range, new StylePatch(format ?? NumberFormat.General, null)).Change;

    /// <summary>Sets the horizontal alignment of cells. No Value changes.</summary>
    public SheetChange SetAlignment(IEnumerable<CellAddress> addresses, HorizontalAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return StyleCells(addresses, new StylePatch(null, CheckAlignment(alignment)), null).Change;
    }

    /// <summary>Sets one cell's horizontal alignment.</summary>
    public SheetChange SetAlignment(CellAddress address, HorizontalAlignment alignment) => SetAlignment([address], alignment);

    /// <summary>
    /// Sets the horizontal alignment of a range, recording whole columns and whole rows as one
    /// entry each, as <see cref="SetFormat(CellRange, NumberFormat?)"/> does. No Value changes.
    /// </summary>
    public SheetChange SetAlignment(CellRange range, HorizontalAlignment alignment) =>
        ApplyStyle(range, new StylePatch(null, CheckAlignment(alignment))).Change;

    /// <summary>
    /// Sets a number format, an alignment or both on several ranges at once, each range recorded
    /// as <see cref="SetFormat(CellRange, NumberFormat?)"/> records one: whole columns and whole
    /// rows as one entry each (ADR-0046, ADR-0047). Here <see langword="null"/> leaves that
    /// property as it is; <see cref="NumberFormat.General"/> sets General. No Value changes.
    /// </summary>
    /// <exception cref="ArgumentException">There is no range, or the style sets nothing.</exception>
    public SheetChange SetStyle(IEnumerable<CellRange> ranges, NumberFormat? format, HorizontalAlignment? alignment)
    {
        var (list, patch) = CheckStyle(ranges, format, alignment);
        return ApplyStyles(list, patch).Change;
    }

    /// <summary>The ranges and the patch of a style set on several ranges, checked.</summary>
    internal static (IReadOnlyList<CellRange> Ranges, StylePatch Patch) CheckStyle(IEnumerable<CellRange> ranges, NumberFormat? format, HorizontalAlignment? alignment)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var list = ranges.ToList();
        if (list.Count == 0) throw new ArgumentException("A style is set on at least one range.", nameof(ranges));
        if (format is null && alignment is null) throw new ArgumentException("The style sets neither a number format nor an alignment.", nameof(format));
        if (alignment is { } a) CheckAlignment(a);
        return (list, new StylePatch(format, alignment));
    }

    /// <summary>What a style set on several ranges did: each range's outcome, in the order they were set.</summary>
    internal sealed record StylesOutcome(SheetChange Change, IReadOnlyList<StyleOutcome> Parts);

    /// <summary>Sets <paramref name="patch"/> on each range in turn, as one change.</summary>
    internal StylesOutcome ApplyStyles(IReadOnlyList<CellRange> ranges, StylePatch patch)
    {
        var parts = ranges.Select(range => ApplyStyle(range, patch)).ToList();
        return new StylesOutcome(SheetChange.Merge(parts.Select(p => p.Change)), parts);
    }

    /// <summary>Undoes a style set on several ranges: each range's part undone in the reverse of the order it was set, so every level ends exactly as it was.</summary>
    internal SheetChange UndoStyles(StylesOutcome outcome)
    {
        var changes = new List<SheetChange>();
        for (var i = outcome.Parts.Count - 1; i >= 0; i--) changes.Add(UndoStyle(outcome.Parts[i]));
        return SheetChange.Merge(changes);
    }

    private static HorizontalAlignment CheckAlignment(HorizontalAlignment alignment) =>
        Enum.IsDefined(alignment) ? alignment : throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Not an alignment.");

    private static int CheckRow(int row) =>
        (uint)row < RowCount ? row : throw new ArgumentOutOfRangeException(nameof(row), row, $"A row is 0 to {RowCount - 1}.");

    private static int CheckColumn(int column) =>
        (uint)column < ColumnCount ? column : throw new ArgumentOutOfRangeException(nameof(column), column, $"A column is 0 to {ColumnCount - 1}.");

    /// <summary>What a cell takes when it sets no format of its own: its row's, else its column's.</summary>
    private NumberFormat InheritedFormat(CellAddress address) =>
        (_rowStyles.TryGetValue(address.Row, out var row) ? row.Format : null)
        ?? (_columnStyles.TryGetValue(address.Column, out var column) ? column.Format : null)
        ?? NumberFormat.General;

    /// <summary>What a cell takes when it sets no alignment of its own: its row's, else its column's.</summary>
    private HorizontalAlignment InheritedAlignment(CellAddress address) =>
        (_rowStyles.TryGetValue(address.Row, out var row) ? row.Alignment : null)
        ?? (_columnStyles.TryGetValue(address.Column, out var column) ? column.Alignment : null)
        ?? HorizontalAlignment.General;

    /// <summary>What a formatting command did, and what undoing it puts back.</summary>
    internal sealed record StyleOutcome(
        SheetChange Change,
        IReadOnlyList<(CellAddress Address, CellState State)> Before,
        Dictionary<int, AxisStyle> RowsBefore,
        Dictionary<int, AxisStyle> ColumnsBefore);

    /// <summary>
    /// A format or an alignment set on a range, as Excel sets it (ADR-0047). Whole columns set the
    /// column level, and every cell of those columns that sets the property itself, or whose row
    /// does, is given it so that it shows it; whole rows set the row level, and the cells of those
    /// rows that set it themselves take it. The whole Sheet is whole columns, with the rows' own
    /// setting of the property cleared. Any other range sets its cells.
    /// </summary>
    internal StyleOutcome ApplyStyle(CellRange range, StylePatch patch)
    {
        var rowsBefore = new Dictionary<int, AxisStyle>(_rowStyles);
        var columnsBefore = new Dictionary<int, AxisStyle>(_columnStyles);
        var wholeColumns = range.IsWholeColumns;
        var wholeRows = range.IsWholeRows;
        if (!wholeColumns && !wholeRows) return StyleCells(range.Cells(), patch, (rowsBefore, columnsBefore));

        var touched = new List<CellAddress>();
        if (wholeColumns)
        {
            if (wholeRows)
            {
                foreach (var row in _rowStyles.Keys.ToList()) SetAxis(_rowStyles, row, Clear(_rowStyles[row], patch));
            }
            for (var column = range.First.Column; column <= range.Last.Column; column++)
            {
                var style = _columnStyles.GetValueOrDefault(column);
                SetAxis(_columnStyles, column, new AxisStyle(
                    patch.Format is { } f ? (f.IsGeneral ? null : f) : style.Format,
                    patch.Alignment is { } a ? (a == HorizontalAlignment.General ? null : a) : style.Alignment));
            }
            touched.AddRange(_cells.Keys.Where(a => a.Column >= range.First.Column && a.Column <= range.Last.Column));
            foreach (var (row, style) in _rowStyles)
            {
                if ((patch.Format is null || style.Format is null) && (patch.Alignment is null || style.Alignment is null)) continue;
                for (var column = range.First.Column; column <= range.Last.Column; column++) touched.Add(new CellAddress(row, column));
            }
        }
        else
        {
            var formattedColumns = patch.Format is not null && _columnStyles.Values.Any(s => s.Format is not null);
            var alignedColumns = patch.Alignment is not null && _columnStyles.Values.Any(s => s.Alignment is not null);
            for (var row = range.First.Row; row <= range.Last.Row; row++)
            {
                var style = _rowStyles.GetValueOrDefault(row);
                // General on a row is recorded only where a column's format would otherwise show through.
                SetAxis(_rowStyles, row, new AxisStyle(
                    patch.Format is { } f ? (f.IsGeneral && !formattedColumns ? null : f) : style.Format,
                    patch.Alignment is { } a ? (a == HorizontalAlignment.General && !alignedColumns ? null : a) : style.Alignment));
            }
            touched.AddRange(_cells.Keys.Where(a => a.Row >= range.First.Row && a.Row <= range.Last.Row));
        }
        return StyleCells(touched, patch, (rowsBefore, columnsBefore));
    }

    private static AxisStyle Clear(AxisStyle style, StylePatch patch) =>
        new(patch.Format is null ? style.Format : null, patch.Alignment is null ? style.Alignment : null);

    private static void SetAxis(Dictionary<int, AxisStyle> styles, int index, AxisStyle style)
    {
        if (style.IsEmpty) styles.Remove(index);
        else styles[index] = style;
    }

    /// <summary>
    /// Gives each cell the patched properties, recorded on the cell only where they differ from
    /// what its row or column already gives it, so a cell sets nothing it would take anyway.
    /// </summary>
    private StyleOutcome StyleCells(IEnumerable<CellAddress> addresses, StylePatch patch, (Dictionary<int, AxisStyle> Rows, Dictionary<int, AxisStyle> Columns)? axesBefore)
    {
        var before = new Dictionary<CellAddress, CellState>();
        var rows = new SortedSet<int>(axesBefore is null ? [] : RowsAffectedByAxes(axesBefore.Value.Rows, axesBefore.Value.Columns));
        foreach (var address in addresses)
        {
            if (before.ContainsKey(address)) continue;
            before[address] = StateOf(address).Recorded;
            var existing = _cells.TryGetValue(address, out var cell);
            cell ??= new Cell(address);
            var changed = false;
            if (patch.Format is { } format)
            {
                var own = format.Equals(InheritedFormat(address)) ? null : format;
                if (!Equals(cell.Format, own))
                {
                    cell.Format = own;
                    changed = true;
                }
            }
            if (patch.Alignment is { } alignment)
            {
                HorizontalAlignment? own = alignment == InheritedAlignment(address) ? null : alignment;
                if (cell.Alignment != own)
                {
                    cell.Alignment = own;
                    changed = true;
                }
            }
            if (cell.IsEmpty && cell.Value is null) _cells.Remove(address);
            else if (!existing) _cells[address] = cell;
            if (changed || (existing && axesBefore is not null)) rows.Add(address.Row);
        }
        var change = rows.Count == 0 ? SheetChange.None : new SheetChange([], [], [.. rows]);
        return new StyleOutcome(change, [.. before.Select(p => (p.Key, p.Value))],
            axesBefore?.Rows ?? new Dictionary<int, AxisStyle>(_rowStyles),
            axesBefore?.Columns ?? new Dictionary<int, AxisStyle>(_columnStyles));
    }

    /// <summary>The rows holding a cell whose Value shows under a row or column style that differs from <paramref name="rows"/> and <paramref name="columns"/>.</summary>
    private IEnumerable<int> RowsAffectedByAxes(Dictionary<int, AxisStyle> rows, Dictionary<int, AxisStyle> columns)
    {
        var changedRows = rows.Keys.Concat(_rowStyles.Keys).Where(r => rows.GetValueOrDefault(r) != _rowStyles.GetValueOrDefault(r)).ToHashSet();
        var changedColumns = columns.Keys.Concat(_columnStyles.Keys).Where(c => columns.GetValueOrDefault(c) != _columnStyles.GetValueOrDefault(c)).ToHashSet();
        if (changedRows.Count == 0 && changedColumns.Count == 0) return [];
        return _cells.Keys.Where(a => changedRows.Contains(a.Row) || changedColumns.Contains(a.Column)).Select(a => a.Row);
    }

    /// <summary>Undoes a formatting command: the row and column levels, then the cells, exactly as they were.</summary>
    internal SheetChange UndoStyle(StyleOutcome outcome)
    {
        var rowsNow = _rowStyles;
        var columnsNow = _columnStyles;
        _rowStyles = new Dictionary<int, AxisStyle>(outcome.RowsBefore);
        _columnStyles = new Dictionary<int, AxisStyle>(outcome.ColumnsBefore);
        var rows = new SortedSet<int>(RowsAffectedByAxes(rowsNow, columnsNow));
        var change = Restore(outcome.Before);
        rows.UnionWith(change.Rows);
        return rows.Count == 0 ? SheetChange.None : new SheetChange(change.ValueChanges, change.Recalculated, [.. rows]);
    }

    /// <summary>
    /// The row (or column) levels after a structural edit: moved with their rows (columns), those
    /// deleted or pushed off the edge dropped, and inserted rows (columns) given the level of the
    /// one before them when <paramref name="formatInserted"/> (ADR-0046).
    /// </summary>
    private void ShiftAxisStyles(StructuralEdit edit, bool formatInserted)
    {
        var rows = edit.Axis == SheetAxis.Rows;
        var styles = rows ? _rowStyles : _columnStyles;
        var shifted = new Dictionary<int, AxisStyle>();
        foreach (var (index, style) in styles)
        {
            var to = edit.Move(rows ? new CellAddress(index, 0) : new CellAddress(0, index));
            if (to is { } moved) shifted[rows ? moved.Row : moved.Column] = style;
        }
        if (edit.IsInsert && formatInserted && edit.Start > 0 && styles.TryGetValue(edit.Start - 1, out var above))
        {
            for (var i = 0; i < edit.Count; i++) shifted[edit.Start + i] = above;
        }
        if (rows) _rowStyles = shifted;
        else _columnStyles = shifted;
    }

    /// <summary>The row or column levels as a Sheet Document records them: adjacent indexes set alike are one run.</summary>
    private static IReadOnlyList<SheetDocumentAxisStyle> Runs(Dictionary<int, AxisStyle> styles)
    {
        var runs = new List<SheetDocumentAxisStyle>();
        foreach (var index in styles.Keys.Order())
        {
            var style = styles[index];
            if (runs.Count > 0 && runs[^1] is var last && last.Last == index - 1 && Equals(last.Format, style.Format) && last.Alignment == style.Alignment)
            {
                runs[^1] = last with { Last = index };
            }
            else
            {
                runs.Add(new SheetDocumentAxisStyle(index, index, style.Format, style.Alignment));
            }
        }
        return runs;
    }
}
