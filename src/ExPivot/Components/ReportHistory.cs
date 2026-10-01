using ExGrid;
using ExGrid.Cells;
using ExPivot.Engine;

namespace ExPivot.Components;

/// <summary>
/// The reports of the recent data versions under one layout, newest first: what ExPivot answers
/// the report grid's <c>CellChangedAt</c> from (ADR-0067/0068). Immutable — a data version makes a
/// new history, and with it a new delegate, whose identity is the grid's change signal; a history
/// the grid still holds keeps answering as it did. Only data extends it: a new layout, a sort, a
/// collapse, a form, Show Values As, a format or new words start a new one, which marks nothing.
///
/// <para>A value cell is marked at the time of the newest version whose painted text differs from
/// the version before it — a cell with no counterpart there, its row or its column new, included —
/// so a change the number format hides is never marked. The cells are compared by what they stand
/// for: the row's role, Value Field and Items, and the column's name, which carries its own role,
/// Items and Value Field. The work is the grid's question per painted cell, and it is kept light and
/// lazy: a row is found by its position in the order the grid paints, a version's rows by key only
/// when its rows differ from the next one's, and each text once per report.</para>
/// </summary>
internal sealed class ReportHistory
{
    private readonly ReportVersion[] _versions;

    private ReportHistory(ReportVersion[] versions, bool canMark)
    {
        _versions = versions;
        // Made once per history: the delegate's identity is what tells the grid to ask again.
        Answer = canMark ? ChangedAt : null;
    }

    /// <summary>The report on screen.</summary>
    public PivotReport Current => _versions[0].Report;

    /// <summary>How many versions are held, the report on screen included.</summary>
    public int Count => _versions.Length;

    /// <summary>What the grid is handed: null while nothing can be marked — no version before
    /// the one on screen, or a duration of zero — so the grid asks nothing (DC-1).</summary>
    public CellChangeOf<PivotReportRow>? Answer { get; }

    /// <summary>A history of one report, which marks nothing: the first report, and every report a
    /// change other than data laid out.</summary>
    public static ReportHistory Start(PivotReport report, DateTimeOffset at)
        => new([new ReportVersion(report, at, sameRowsAsPrevious: false, previous: null)], canMark: false);

    /// <summary>
    /// The history with <paramref name="report"/>, the next data version, laid out under the same
    /// layout and words as the one on screen. It holds the versions whose marks can still show at
    /// <paramref name="at"/> — each version's marks last <paramref name="duration"/> from its time —
    /// and, as the baseline of the oldest of them, the one before it.
    /// </summary>
    public ReportHistory Extend(PivotReport report, DateTimeOffset at, bool sameRowsAsPrevious, TimeSpan duration)
    {
        var kept = new List<ReportVersion>(_versions.Length + 1)
        {
            new(report, at, sameRowsAsPrevious, _versions[0]),
        };
        foreach (var version in _versions)
        {
            kept.Add(version);
            // This version's own marks have ended, so nothing before it can matter: it stays only
            // as the baseline of the version after it.
            if (ChangeHighlightRules.EndOf(version.At, duration) <= at)
                break;
        }
        return new ReportHistory([.. kept], canMark: duration > TimeSpan.Zero);
    }

    /// <summary>
    /// When the shown value of the cell at (<paramref name="row"/>, <paramref name="column"/>) last
    /// changed with the data, or null (ADR-0068): asked by the grid for each painted value cell.
    /// </summary>
    private DateTimeOffset? ChangedAt(PivotReportRow row, GridColumn<PivotReportRow> column)
    {
        var versions = _versions;
        var v = 0;
        // The report on screen, normally; a row of an older one only while the grid catches up.
        while (v < versions.Length && !ReferenceEquals(versions[v].Report, row.Report))
            v++;
        if (v >= versions.Length - 1)
            return null;
        var i = versions[v].IndexOf(row);
        var j = versions[v].ColumnIndexOf(column);
        if (i < 0 || j < 0)
            return null;
        for (; v < versions.Length - 1; v++)
        {
            var newer = versions[v];
            var older = versions[v + 1];
            var olderRow = newer.SameRowsAsPrevious ? i : older.IndexOfKey(newer.KeyAt(i));
            var olderColumn = newer.SameColumnsAsPrevious ? j : older.ColumnIndexOf(newer.Report.ValueColumns[j].Name);
            if (olderRow < 0 || olderColumn < 0
                || !string.Equals(newer.TextAt(i, j), older.TextAt(olderRow, olderColumn), StringComparison.Ordinal))
            {
                return newer.At;
            }
            i = olderRow;
            j = olderColumn;
        }
        return null;
    }
}

/// <summary>One data version of the report: the report, when its answer arrived, how its rows and
/// columns line up with the version before it, and what is looked up in it, built when first
/// needed.</summary>
internal sealed class ReportVersion
{
    private readonly Dictionary<string, int> _columnsByName;
    private readonly Dictionary<GridColumn<PivotReportRow>, int> _columns = new(ReferenceEqualityComparer.Instance);
    private Dictionary<PivotReportRow, int>? _rowsByInstance;
    private Dictionary<RowKey, int>? _rowsByKey;
    private Dictionary<int, RowKey>? _keys;
    private int _hint;

    public ReportVersion(PivotReport report, DateTimeOffset at, bool sameRowsAsPrevious, ReportVersion? previous)
    {
        Report = report;
        At = at;
        SameRowsAsPrevious = sameRowsAsPrevious;
        var columns = report.ValueColumns;
        _columnsByName = new Dictionary<string, int>(columns.Count, StringComparer.Ordinal);
        for (var j = 0; j < columns.Count; j++)
            _columnsByName[columns[j].Name] = j;
        SameColumnsAsPrevious = previous is not null && SameColumns(columns, previous.Report.ValueColumns);
    }

    public PivotReport Report { get; }

    /// <summary>When the answer this report was laid out from arrived: the change time of the
    /// cells it marks.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Whether its rows are the previous version's, in the same order (ADR-0011's test,
    /// which the Row Sequence Version already made): a row then stands where it stood.</summary>
    public bool SameRowsAsPrevious { get; }

    /// <summary>Whether its value columns are the previous version's, in the same order.</summary>
    public bool SameColumnsAsPrevious { get; }

    private static bool SameColumns(IReadOnlyList<PivotReportColumn> columns, IReadOnlyList<PivotReportColumn> previous)
    {
        if (columns.Count != previous.Count)
            return false;
        for (var j = 0; j < columns.Count; j++)
        {
            if (!string.Equals(columns[j].Name, previous[j].Name, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>The painted text of a value cell: its engine text, and nothing for an empty
    /// cell, as the grid paints it.</summary>
    public string TextAt(int row, int column) => Report.Rows[row].ValueAt(column)?.Text ?? "";

    /// <summary>A grid column's index among the value columns, or −1 for one that is not.</summary>
    public int ColumnIndexOf(GridColumn<PivotReportRow> column)
    {
        if (!_columns.TryGetValue(column, out var index))
        {
            index = ColumnIndexOf(column.Name);
            _columns[column] = index;
        }
        return index;
    }

    /// <summary>A value column's index by its name, or −1.</summary>
    public int ColumnIndexOf(string name) => _columnsByName.TryGetValue(name, out var index) ? index : -1;

    /// <summary>
    /// A row's position, or −1. The grid asks row after row, in the order it paints them, so the
    /// row asked last, or the one after it, is the answer almost every time; a scroll's first row
    /// finds it through an index of the rows by instance, built once.
    /// </summary>
    public int IndexOf(PivotReportRow row)
    {
        var rows = Report.Rows;
        var hint = _hint;
        if ((uint)hint < (uint)rows.Count && ReferenceEquals(rows[hint], row))
            return hint;
        if ((uint)(hint + 1) < (uint)rows.Count && ReferenceEquals(rows[hint + 1], row))
            return _hint = hint + 1;
        if (_rowsByInstance is null)
        {
            _rowsByInstance = new Dictionary<PivotReportRow, int>(rows.Count, ReferenceEqualityComparer.Instance);
            for (var i = 0; i < rows.Count; i++)
                _rowsByInstance[rows[i]] = i;
        }
        if (!_rowsByInstance.TryGetValue(row, out var index))
            return -1;
        return _hint = index;
    }

    /// <summary>What the row at <paramref name="index"/> stands for.</summary>
    public RowKey KeyAt(int index)
    {
        _keys ??= [];
        if (!_keys.TryGetValue(index, out var key))
        {
            key = RowKey.Of(Report, Report.Rows[index]);
            _keys[index] = key;
        }
        return key;
    }

    /// <summary>The position of the row that stands for <paramref name="key"/>, or −1: asked only
    /// of a version whose rows the next version does not share.</summary>
    public int IndexOfKey(RowKey key)
    {
        if (_rowsByKey is null)
        {
            var rows = Report.Rows;
            _rowsByKey = new Dictionary<RowKey, int>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                _rowsByKey.TryAdd(RowKey.Of(Report, rows[i]), i);
        }
        return _rowsByKey.TryGetValue(key, out var index) ? index : -1;
    }
}

/// <summary>What a report row stands for, across the reports of one layout: its role, its Value
/// Field and its Items, outermost first — the test <see cref="PivotReport.HasSameRowsAs"/> applies
/// position by position. The row fields are the layout's, which a history shares.</summary>
internal readonly struct RowKey : IEquatable<RowKey>
{
    private readonly PivotItemKey[] _items;
    private readonly int _hash;

    private RowKey(PivotRowRole role, int valueField, PivotItemKey[] items)
    {
        Role = role;
        ValueField = valueField;
        _items = items;
        var hash = new HashCode();
        hash.Add(role);
        hash.Add(valueField);
        foreach (var item in items)
            hash.Add(item);
        _hash = hash.ToHashCode();
    }

    public PivotRowRole Role { get; }

    public int ValueField { get; }

    public static RowKey Of(PivotReport report, PivotReportRow row)
    {
        var path = report.RowPath(row);
        var items = new PivotItemKey[path.Count];
        for (var i = 0; i < items.Length; i++)
            items[i] = path[i].Item;
        return new RowKey(row.Role, row.ValueField, items);
    }

    public bool Equals(RowKey other)
    {
        if (Role != other.Role || ValueField != other.ValueField || _items.Length != other._items.Length)
            return false;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!_items[i].Equals(other._items[i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is RowKey other && Equals(other);

    public override int GetHashCode() => _hash;
}
