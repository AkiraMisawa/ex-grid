using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// The parts of a Cell Format recorded on a whole row or a whole column (ADR-0047, ADR-0063):
/// <see langword="null"/> where that level records nothing.
/// </summary>
internal readonly record struct AxisFormat(NumberFormat? NumberFormat, HorizontalAlignment? Alignment, CellFont? Font, CellFill? Fill, CellBorders? Borders)
{
    public bool IsEmpty => NumberFormat is null && Alignment is null && Font is null && Fill is null && Borders is null;

    /// <summary>What the level shows over <paramref name="under"/>: each part it records, else <paramref name="under"/>'s.</summary>
    public CellFormat Over(CellFormat under) =>
        new(NumberFormat ?? under.NumberFormat, Alignment ?? under.Alignment, Font ?? under.Font, Fill ?? under.Fill, Borders ?? under.Borders);

    /// <summary>
    /// The level with each part <paramref name="change"/> sets taken from <paramref name="next"/>,
    /// and recorded as nothing where it equals what shows under the level anyway.
    /// </summary>
    public AxisFormat With(CellFormatChange change, CellFormat next, CellFormat under) => new(
        change.NumberFormat is null ? NumberFormat : next.NumberFormat.Equals(under.NumberFormat) ? null : next.NumberFormat,
        change.Alignment is null ? Alignment : Sheet.Own(next.Alignment, under.Alignment),
        change.SetsFont ? Sheet.Own(next.Font, under.Font) : Font,
        change.Fill is null ? Fill : Sheet.Own(next.Fill, under.Fill),
        change.SetsBorders ? Sheet.Own(next.Borders, under.Borders) : Borders);
}

public sealed partial class Sheet
{
    /// <summary>The Cell Formats recorded on whole rows, by row; a row absent records nothing (ADR-0047).</summary>
    private Dictionary<int, AxisFormat> _rowFormats = [];

    /// <summary>The Cell Formats recorded on whole columns, by column; a column absent records nothing (ADR-0047).</summary>
    private Dictionary<int, AxisFormat> _columnFormats = [];

    /// <summary>
    /// The cell's Cell Format as it shows: each part the cell's own, else its row's, else its
    /// column's, else the part's default — cell over row over column, as in Excel (ADR-0047,
    /// ADR-0063, SH-38). Its Borders are the edges as shown, which the cells beside it share
    /// (<see cref="GetBorders"/>).
    /// </summary>
    public CellFormat GetCellFormat(CellAddress address) => OwnFormat(address) with { Borders = GetBorders(address) };

    /// <summary>
    /// The cell's Cell Format as it records it, cell over row over column, its Borders its own four
    /// sides rather than the edges shown: what a command patches, and what a copy carries.
    /// </summary>
    private CellFormat OwnFormat(CellAddress address) =>
        _cells.TryGetValue(address, out var cell) ? cell.Over(Inherited(address)) : Inherited(address);

    /// <summary>
    /// The cell's Number Format, as it takes effect: the cell's own, else its row's, else its
    /// column's, else <see cref="NumberFormat.General"/> — cell over row over column, as in Excel
    /// (ADR-0047).
    /// </summary>
    public NumberFormat GetNumberFormat(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.NumberFormat : null)
        ?? _rowFormats.GetValueOrDefault(address.Row).NumberFormat
        ?? _columnFormats.GetValueOrDefault(address.Column).NumberFormat
        ?? NumberFormat.General;

    /// <summary>The cell's horizontal alignment setting, as it takes effect: cell over row over column (ADR-0047).</summary>
    public HorizontalAlignment GetAlignment(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Alignment : null)
        ?? _rowFormats.GetValueOrDefault(address.Row).Alignment
        ?? _columnFormats.GetValueOrDefault(address.Column).Alignment
        ?? HorizontalAlignment.General;

    /// <summary>The cell's Font, as it takes effect: cell over row over column (ADR-0063, SH-38).</summary>
    public CellFont GetFont(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Font : null)
        ?? _rowFormats.GetValueOrDefault(address.Row).Font
        ?? _columnFormats.GetValueOrDefault(address.Column).Font
        ?? CellFont.Default;

    /// <summary>The cell's Fill, as it takes effect: cell over row over column (ADR-0063, SH-38).</summary>
    public CellFill GetFill(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Fill : null)
        ?? _rowFormats.GetValueOrDefault(address.Row).Fill
        ?? _columnFormats.GetValueOrDefault(address.Column).Fill
        ?? CellFill.None;

    /// <summary>
    /// The line on each of the cell's four sides as it shows, which is the line on the edge it shares
    /// with the cell beside it, read the same from either cell (ADR-0063; the twelfth Windows run).
    /// Each cell records its own four sides, cell over row over column. Where both cells record a
    /// line on the edge, the upper cell's is shown, or the left cell's for a vertical edge; where
    /// only one does, that one. A side on the Sheet's outer edge is the cell's own.
    /// </summary>
    public CellBorders GetBorders(CellAddress address)
    {
        var own = OwnSides(address);
        return new CellBorders(
            address.Row > 0 ? Edge(OwnSides(new CellAddress(address.Row - 1, address.Column)).Bottom, own.Top) : own.Top,
            own.Bottom.IsNone && address.Row < RowCount - 1 ? OwnSides(new CellAddress(address.Row + 1, address.Column)).Top : own.Bottom,
            address.Column > 0 ? Edge(OwnSides(new CellAddress(address.Row, address.Column - 1)).Right, own.Left) : own.Left,
            own.Right.IsNone && address.Column < ColumnCount - 1 ? OwnSides(new CellAddress(address.Row, address.Column + 1)).Left : own.Right);
    }

    /// <summary>The cell's own four sides, as it records them: cell over row over column (ADR-0063, SH-38).</summary>
    private CellBorders OwnSides(CellAddress address) =>
        (_cells.TryGetValue(address, out var cell) ? cell.Borders : null)
        ?? _rowFormats.GetValueOrDefault(address.Row).Borders
        ?? _columnFormats.GetValueOrDefault(address.Column).Borders
        ?? CellBorders.None;

    /// <summary>
    /// The line shown on an edge two cells record a side of: the upper cell's (the left cell's)
    /// where it records one, else the lower cell's (the right cell's) — the twelfth Windows run,
    /// cases 1, 3, 6, 7 and 9.
    /// </summary>
    private static BorderLine Edge(BorderLine upperOrLeft, BorderLine lowerOrRight) => upperOrLeft.IsNone ? lowerOrRight : upperOrLeft;

    /// <summary>The Number Format recorded on the whole row, or <see langword="null"/> when none is (ADR-0047).</summary>
    public NumberFormat? GetRowNumberFormat(int row) => _rowFormats.TryGetValue(CheckRow(row), out var level) ? level.NumberFormat : null;

    /// <summary>The Number Format recorded on the whole column, or <see langword="null"/> when none is (ADR-0047).</summary>
    public NumberFormat? GetColumnNumberFormat(int column) => _columnFormats.TryGetValue(CheckColumn(column), out var level) ? level.NumberFormat : null;

    /// <summary>The alignment recorded on the whole row, or <see langword="null"/> when none is (ADR-0047).</summary>
    public HorizontalAlignment? GetRowAlignment(int row) => _rowFormats.TryGetValue(CheckRow(row), out var level) ? level.Alignment : null;

    /// <summary>The alignment recorded on the whole column, or <see langword="null"/> when none is (ADR-0047).</summary>
    public HorizontalAlignment? GetColumnAlignment(int column) => _columnFormats.TryGetValue(CheckColumn(column), out var level) ? level.Alignment : null;

    /// <summary>Sets the Number Format of cells; <see langword="null"/> is <see cref="NumberFormat.General"/>. No Value changes.</summary>
    public SheetChange SetNumberFormat(IEnumerable<CellAddress> addresses, NumberFormat? format)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return FormatCells(addresses, new CellFormatChange { NumberFormat = format ?? NumberFormat.General }, null, null, null).Change;
    }

    /// <summary>Sets one cell's Number Format.</summary>
    public SheetChange SetNumberFormat(CellAddress address, NumberFormat? format) => SetNumberFormat([address], format);

    /// <summary>
    /// Sets the Number Format of a range; <see langword="null"/> is <see cref="NumberFormat.General"/>.
    /// Whole columns (<see cref="CellRange.IsWholeColumns"/>) record one entry per column, and
    /// whole rows one per row, never one per cell (ADR-0047); the cells inside that record their
    /// own Number Format take the new one, as Excel's do. Any other range sets each of its cells.
    /// No Value changes.
    /// </summary>
    public SheetChange SetNumberFormat(CellRange range, NumberFormat? format) =>
        ApplyCellFormat(range, new CellFormatChange { NumberFormat = format ?? NumberFormat.General }).Change;

    /// <summary>Sets the horizontal alignment of cells. No Value changes.</summary>
    public SheetChange SetAlignment(IEnumerable<CellAddress> addresses, HorizontalAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return FormatCells(addresses, new CellFormatChange { Alignment = CheckAlignment(alignment) }, null, null, null).Change;
    }

    /// <summary>Sets one cell's horizontal alignment.</summary>
    public SheetChange SetAlignment(CellAddress address, HorizontalAlignment alignment) => SetAlignment([address], alignment);

    /// <summary>
    /// Sets the horizontal alignment of a range, recording whole columns and whole rows as one
    /// entry each, as <see cref="SetNumberFormat(CellRange, NumberFormat?)"/> does. No Value changes.
    /// </summary>
    public SheetChange SetAlignment(CellRange range, HorizontalAlignment alignment) =>
        ApplyCellFormat(range, new CellFormatChange { Alignment = CheckAlignment(alignment) }).Change;

    /// <summary>
    /// Sets the parts <paramref name="change"/> names on several ranges at once (ADR-0046,
    /// ADR-0063), each range as <see cref="SetNumberFormat(CellRange, NumberFormat?)"/> records
    /// one: whole columns and whole rows as one entry each. Every other part stays as each cell
    /// has it. The change's Borders are relative to each range, so each range gets its own
    /// outline. No Value changes.
    /// </summary>
    /// <exception cref="ArgumentException">There is no range, or the change sets nothing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The alignment is not one.</exception>
    public SheetChange SetCellFormat(IEnumerable<CellRange> ranges, CellFormatChange change) =>
        ApplyCellFormats(CheckCellFormat(ranges, change), change).Change;

    /// <summary>The ranges a change is set on, checked with the change.</summary>
    internal static IReadOnlyList<CellRange> CheckCellFormat(IEnumerable<CellRange> ranges, CellFormatChange change)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(change);
        var list = ranges.ToList();
        if (list.Count == 0) throw new ArgumentException("A Cell Format is set on at least one range.", nameof(ranges));
        if (change.IsEmpty) throw new ArgumentException("The change sets no part of a Cell Format.", nameof(change));
        if (change.Alignment is { } a) CheckAlignment(a);
        return list;
    }

    /// <summary>What a change set on several ranges did: each range's outcome, in the order they were set.</summary>
    internal sealed record CellFormatsOutcome(SheetChange Change, IReadOnlyList<CellFormatOutcome> Parts);

    /// <summary>Sets <paramref name="change"/> on each range in turn, as one change.</summary>
    internal CellFormatsOutcome ApplyCellFormats(IReadOnlyList<CellRange> ranges, CellFormatChange change)
    {
        var parts = ranges.Select(range => ApplyCellFormat(range, change)).ToList();
        return new CellFormatsOutcome(SheetChange.Merge(parts.Select(p => p.Change)), parts);
    }

    /// <summary>Undoes a change set on several ranges: each range's part undone in the reverse of the order it was set, so every level ends exactly as it was.</summary>
    internal SheetChange UndoCellFormats(CellFormatsOutcome outcome)
    {
        var changes = new List<SheetChange>();
        for (var i = outcome.Parts.Count - 1; i >= 0; i--) changes.Add(UndoCellFormat(outcome.Parts[i]));
        return SheetChange.Merge(changes);
    }

    private static HorizontalAlignment CheckAlignment(HorizontalAlignment alignment) =>
        Enum.IsDefined(alignment) ? alignment : throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Not an alignment.");

    private static int CheckRow(int row) =>
        (uint)row < RowCount ? row : throw new ArgumentOutOfRangeException(nameof(row), row, $"A row is 0 to {RowCount - 1}.");

    private static int CheckColumn(int column) =>
        (uint)column < ColumnCount ? column : throw new ArgumentOutOfRangeException(nameof(column), column, $"A column is 0 to {ColumnCount - 1}.");

    /// <summary>What a cell shows where it records nothing of its own: its row's, else its column's, else the defaults.</summary>
    private CellFormat Inherited(CellAddress address) =>
        _rowFormats.GetValueOrDefault(address.Row).Over(_columnFormats.GetValueOrDefault(address.Column).Over(CellFormat.Default));

    /// <summary><paramref name="value"/>, or <see langword="null"/> where it equals what shows under it anyway.</summary>
    internal static T? Own<T>(T value, T under)
        where T : struct => EqualityComparer<T>.Default.Equals(value, under) ? null : value;

    /// <summary>What a formatting command did, and what undoing it puts back.</summary>
    internal sealed record CellFormatOutcome(
        SheetChange Change,
        IReadOnlyList<(CellAddress Address, CellState State)> Before,
        Dictionary<int, AxisFormat> RowsBefore,
        Dictionary<int, AxisFormat> ColumnsBefore);

    /// <summary>
    /// A change set on a range, as Excel sets it (ADR-0047, ADR-0063): on the range
    /// (<see cref="ApplyCellFormatOn"/>), and, across each outer edge the change sets, on the cells
    /// beside it at whichever level they lie on, whose record of that edge it clears. So the line
    /// set is the one shown from either cell, and the later setting wins (the eleventh Windows run,
    /// cases 7 and 13; the twelfth run). Undoing it puts back both.
    /// </summary>
    internal CellFormatOutcome ApplyCellFormat(CellRange range, CellFormatChange change)
    {
        var own = ApplyCellFormatOn(range, change);
        if (!change.SetsBorders) return own;
        List<CellFormatOutcome> parts = [own];
        foreach (var (beside, borders) in change.Borders!.Beside(range)) parts.Add(ApplyCellFormatOn(beside, new CellFormatChange { Borders = borders }));
        return Combined(parts);
    }

    /// <summary>
    /// The parts of one change as one outcome: the levels as they were before the first part, and
    /// each cell as it was before the first part that touched it.
    /// </summary>
    private static CellFormatOutcome Combined(List<CellFormatOutcome> parts)
    {
        var seen = new HashSet<CellAddress>();
        return new CellFormatOutcome(
            SheetChange.Merge(parts.Select(p => p.Change)),
            [.. parts.SelectMany(p => p.Before).Where(b => seen.Add(b.Address))],
            parts[0].RowsBefore,
            parts[0].ColumnsBefore);
    }

    /// <summary>
    /// A change set on the range alone. Whole columns record it on the column level; whole rows on
    /// the row level. The whole Sheet is whole columns: the rows' own Number Format, Alignment and
    /// Fill are cleared, and a Font or Borders a row records is patched. A cell a level alone would
    /// not show what it should is given it as its own (<see cref="CellsGivenTheirOwn"/>). Any other
    /// range sets its cells.
    /// </summary>
    private CellFormatOutcome ApplyCellFormatOn(CellRange range, CellFormatChange change)
    {
        var rowsBefore = new Dictionary<int, AxisFormat>(_rowFormats);
        var columnsBefore = new Dictionary<int, AxisFormat>(_columnFormats);
        var wholeColumns = range.IsWholeColumns;
        var wholeRows = range.IsWholeRows;
        if (!wholeColumns && !wholeRows) return FormatCells(range.Cells(), change, range, null, null);

        var touched = CellsGivenTheirOwn(range, change);
        // What each of them records before the levels change under it: a Font or Borders is patched from that.
        var shownBefore = new Dictionary<CellAddress, CellFormat>();
        foreach (var address in touched) shownBefore.TryAdd(address, OwnFormat(address));
        if (wholeColumns)
        {
            if (wholeRows)
            {
                foreach (var row in _rowFormats.Keys.ToList()) SetAxis(_rowFormats, row, UnderWholeSheet(_rowFormats[row], change));
            }
            for (var column = range.First.Column; column <= range.Last.Column; column++)
            {
                // Nothing lies under a column: a part at its default records nothing. The whole
                // Sheet has no outer edge (the twelfth Windows run, case 15).
                var level = _columnFormats.GetValueOrDefault(column);
                var next = change.ApplyTo(level.Over(CellFormat.Default), wholeRows ? PlaceInRange.Inside : new PlaceInRange(false, false, column == range.First.Column, column == range.Last.Column));
                SetAxis(_columnFormats, column, level.With(change, next, CellFormat.Default));
            }
        }
        else
        {
            // General, General alignment or no Fill on a row is recorded only where a column's would otherwise show through.
            var formatted = change.NumberFormat is not null && _columnFormats.Values.Any(f => f.NumberFormat is not null);
            var aligned = change.Alignment is not null && _columnFormats.Values.Any(f => f.Alignment is not null);
            var filled = change.Fill is not null && _columnFormats.Values.Any(f => f.Fill is not null);
            for (var row = range.First.Row; row <= range.Last.Row; row++)
            {
                var level = _rowFormats.GetValueOrDefault(row);
                var next = change.ApplyTo(level.Over(CellFormat.Default), new PlaceInRange(row == range.First.Row, row == range.Last.Row, false, false));
                var recorded = level.With(change, next, CellFormat.Default);
                SetAxis(_rowFormats, row, recorded with
                {
                    NumberFormat = formatted ? next.NumberFormat : recorded.NumberFormat,
                    Alignment = aligned ? next.Alignment : recorded.Alignment,
                    Fill = filled ? next.Fill : recorded.Fill,
                });
            }
        }
        return FormatCells(touched, change, range, shownBefore, (rowsBefore, columnsBefore));
    }

    /// <summary>
    /// The cells a change set on whole columns or whole rows gives a record of their own, because
    /// the levels alone would not show them what they should:
    /// <list type="bullet">
    /// <item>every cell inside that holds anything;</item>
    /// <item>on whole columns short of the whole Sheet, the cells of a row that records a part the
    /// change sets, since a row's hides the column's;</item>
    /// <item>on whole rows, the cells of a column that records a Font or Borders, since the row's is
    /// patched from its own and would hide the column's the cell showed;</item>
    /// <item>on whole rows, each row's cell in column A where the left of column A takes another
    /// line than the inner sides, since an outline over whole rows sets it (the twelfth Windows run,
    /// case 14) and a row's level is the same the whole length of the row.</item>
    /// </list>
    /// No other cell is given its own for an outer edge: whole columns have no top or bottom edge,
    /// whole rows no right edge, and the whole Sheet none (<see cref="PlaceInRange.Of"/>).
    /// </summary>
    private List<CellAddress> CellsGivenTheirOwn(CellRange range, CellFormatChange change)
    {
        var touched = new List<CellAddress>();
        var borders = change.SetsBorders ? change.Borders! : null;
        if (range.IsWholeColumns)
        {
            touched.AddRange(_cells.Keys.Where(a => a.Column >= range.First.Column && a.Column <= range.Last.Column));
            if (range.IsWholeRows) return touched;
            foreach (var (row, level) in _rowFormats)
            {
                if (!change.Touches(level)) continue;
                for (var column = range.First.Column; column <= range.Last.Column; column++) touched.Add(new CellAddress(row, column));
            }
        }
        else
        {
            touched.AddRange(_cells.Keys.Where(a => a.Row >= range.First.Row && a.Row <= range.Last.Row));
            foreach (var (column, level) in _columnFormats)
            {
                if (!(change.SetsFont && level.Font is not null) && !(borders is not null && level.Borders is not null)) continue;
                for (var row = range.First.Row; row <= range.Last.Row; row++) touched.Add(new CellAddress(row, column));
            }
            if (borders is not null && borders.Left != borders.InsideVertical)
            {
                for (var row = range.First.Row; row <= range.Last.Row; row++) touched.Add(new CellAddress(row, range.First.Column));
            }
        }
        return touched;
    }

    /// <summary>
    /// A row's level when a change is set on the whole Sheet: the Number Format, Alignment and Fill
    /// it sets are now every column's, so the row's own are cleared; a Font or Borders the row
    /// records hides the columns' from its cells, so it is patched as they take the change. Every
    /// side of its cells is inside the Sheet, which has no outer edge (the twelfth Windows run, case
    /// 15).
    /// </summary>
    private static AxisFormat UnderWholeSheet(AxisFormat level, CellFormatChange change) => new(
        change.NumberFormat is null ? level.NumberFormat : null,
        change.Alignment is null ? level.Alignment : null,
        change.SetsFont && level.Font is { } font ? change.ApplyTo(font) : level.Font,
        change.Fill is null ? level.Fill : null,
        change.SetsBorders && level.Borders is { } borders ? change.Borders!.ApplyTo(borders, PlaceInRange.Inside) : level.Borders);

    private static void SetAxis(Dictionary<int, AxisFormat> levels, int index, AxisFormat level)
    {
        if (level.IsEmpty) levels.Remove(index);
        else levels[index] = level;
    }

    /// <summary>
    /// Gives each cell what it records once <paramref name="change"/> is set on it at its place in
    /// <paramref name="range"/>, patched from what it recorded before (<paramref name="shownBefore"/>,
    /// or now), and recorded on the cell only where it differs from what its row or column gives
    /// it, so a cell records nothing it would show anyway. A changed top or bottom side names the
    /// row across it too (<see cref="RowsAcross"/>).
    /// </summary>
    private CellFormatOutcome FormatCells(
        IEnumerable<CellAddress> addresses,
        CellFormatChange change,
        CellRange? range,
        Dictionary<CellAddress, CellFormat>? shownBefore,
        (Dictionary<int, AxisFormat> Rows, Dictionary<int, AxisFormat> Columns)? axesBefore)
    {
        var before = new Dictionary<CellAddress, CellState>();
        var rows = new SortedSet<int>(axesBefore is null ? [] : RowsAffectedByAxes(axesBefore.Value.Rows, axesBefore.Value.Columns));
        foreach (var address in addresses)
        {
            if (before.ContainsKey(address)) continue;
            before[address] = StateOf(address).Recorded;
            var shown = shownBefore is not null && shownBefore.TryGetValue(address, out var was) ? was : OwnFormat(address);
            var next = change.ApplyTo(shown, range is { } r ? PlaceInRange.Of(r, address) : PlaceInRange.Alone);
            var existing = _cells.TryGetValue(address, out var cell);
            cell ??= new Cell(address);
            var changed = cell.Take(change, next, Inherited(address));
            if (cell.IsEmpty && cell.Value is null) _cells.Remove(address);
            else if (!existing) _cells[address] = cell;
            if (changed || (existing && axesBefore is not null)) rows.Add(address.Row);
            rows.UnionWith(RowsAcross(address, shown.Borders, next.Borders));
        }
        var sheetChange = rows.Count == 0 ? SheetChange.None : new SheetChange([], [], [.. rows]);
        return new CellFormatOutcome(sheetChange, [.. before.Select(p => (p.Key, p.Value))],
            axesBefore?.Rows ?? new Dictionary<int, AxisFormat>(_rowFormats),
            axesBefore?.Columns ?? new Dictionary<int, AxisFormat>(_columnFormats));
    }

    /// <summary>
    /// The rows beside a cell whose painted edge changes with the cell's own sides: the row above
    /// where its top changed, the row below where its bottom did, since the edge it shares with each
    /// is shown from both cells (<see cref="GetBorders"/>).
    /// </summary>
    private static IEnumerable<int> RowsAcross(CellAddress address, CellBorders before, CellBorders after)
    {
        if (before.Top != after.Top && address.Row > 0) yield return address.Row - 1;
        if (before.Bottom != after.Bottom && address.Row < RowCount - 1) yield return address.Row + 1;
    }

    /// <summary>The rows holding a cell that shows under a row or column level that differs from <paramref name="rows"/> and <paramref name="columns"/>.</summary>
    private IEnumerable<int> RowsAffectedByAxes(Dictionary<int, AxisFormat> rows, Dictionary<int, AxisFormat> columns)
    {
        var changedRows = rows.Keys.Concat(_rowFormats.Keys).Where(r => rows.GetValueOrDefault(r) != _rowFormats.GetValueOrDefault(r)).ToHashSet();
        var changedColumns = columns.Keys.Concat(_columnFormats.Keys).Where(c => columns.GetValueOrDefault(c) != _columnFormats.GetValueOrDefault(c)).ToHashSet();
        if (changedRows.Count == 0 && changedColumns.Count == 0) return [];
        return _cells.Keys.Where(a => changedRows.Contains(a.Row) || changedColumns.Contains(a.Column)).Select(a => a.Row);
    }

    /// <summary>Undoes a formatting command: the row and column levels, then the cells, exactly as they were.</summary>
    internal SheetChange UndoCellFormat(CellFormatOutcome outcome)
    {
        var rowsNow = _rowFormats;
        var columnsNow = _columnFormats;
        _rowFormats = new Dictionary<int, AxisFormat>(outcome.RowsBefore);
        _columnFormats = new Dictionary<int, AxisFormat>(outcome.ColumnsBefore);
        var rows = new SortedSet<int>(RowsAffectedByAxes(rowsNow, columnsNow));
        var change = Restore(outcome.Before);
        rows.UnionWith(change.Rows);
        return rows.Count == 0 ? SheetChange.None : new SheetChange(change.ValueChanges, change.Recalculated, [.. rows]);
    }

    /// <summary>
    /// The row (or column) levels after a structural edit: moved with their rows (columns), those
    /// deleted or pushed off the edge dropped, and inserted rows (columns) given the level of the
    /// one before them, all but its Borders, when <paramref name="formatInserted"/> (ADR-0046,
    /// ADR-0063; the twelfth Windows run, cases 10 to 13).
    /// </summary>
    private void ShiftAxisFormats(StructuralEdit edit, bool formatInserted)
    {
        var rows = edit.Axis == SheetAxis.Rows;
        var levels = rows ? _rowFormats : _columnFormats;
        var shifted = new Dictionary<int, AxisFormat>();
        foreach (var (index, level) in levels)
        {
            var to = edit.Move(rows ? new CellAddress(index, 0) : new CellAddress(0, index));
            if (to is { } moved) shifted[rows ? moved.Row : moved.Column] = level;
        }
        if (edit.IsInsert && formatInserted && edit.Start > 0 && levels.TryGetValue(edit.Start - 1, out var above) && above with { Borders = null } is { IsEmpty: false } taken)
        {
            for (var i = 0; i < edit.Count; i++) shifted[edit.Start + i] = taken;
        }
        if (rows) _rowFormats = shifted;
        else _columnFormats = shifted;
    }

    /// <summary>The row or column levels as a Sheet Document records them: adjacent indexes recorded alike are one run.</summary>
    private static IReadOnlyList<SheetDocumentAxisFormat> Runs(Dictionary<int, AxisFormat> levels)
    {
        var runs = new List<(int First, int Last, AxisFormat Level)>();
        foreach (var index in levels.Keys.Order())
        {
            var level = levels[index];
            if (runs.Count > 0 && runs[^1] is var last && last.Last == index - 1 && last.Level == level) runs[^1] = last with { Last = index };
            else runs.Add((index, index, level));
        }
        return [.. runs.Select(r => new SheetDocumentAxisFormat(r.First, r.Last, r.Level.NumberFormat, r.Level.Alignment, r.Level.Font, r.Level.Fill, r.Level.Borders))];
    }
}
