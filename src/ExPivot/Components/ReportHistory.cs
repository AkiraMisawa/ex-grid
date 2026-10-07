using ExGrid;
using ExGrid.Cells;
using ExPivot.Engine;

namespace ExPivot.Components;

/// <summary>
/// The report's Change Highlight under one layout (ADR-0067/0068/0161): when each value cell's
/// painted text last changed with the data, by row key and value column name, and when a row or a
/// column appeared — what ExPivot answers the report grid's <c>CellChangedAt</c> from. It holds the
/// times and never a report: a report is held only while the next is laid out, and compared with it
/// then (<see cref="PivotReport.ChangesSince"/>).
///
/// <para><b>One delegate for the history's life.</b> A history starts with a layout, a sort, a
/// collapse, a form, Show Values As, a format or new words, which mark nothing, and only data extends
/// it. A row renders because its instance is new, not because the delegate is (ADR-0068's note of
/// 2026-10-07), as a bundled source's does (LV-9).</para>
///
/// <para><b>What is kept, and for how long.</b> A mark ends with its time: a time whose mark has
/// ended answers nothing, whether or not it has been let go yet, so a mark never depends on when it
/// was asked for. The times are let go only as new ones are recorded — decided by data, never by a
/// render — and with them the rows and columns that left the report. A key is kept as the newest row
/// recorded under it, so an old report's axis tree is not held by a time that keeps moving on.</para>
/// </summary>
internal sealed class ReportHistory
{
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeSpan> _duration;
    private readonly Dictionary<PivotRowKey, RowTimes> _rows = [];
    private readonly Dictionary<string, DateTimeOffset> _columns = new(StringComparer.Ordinal);
    // Every time recorded, oldest first, so that letting the old ones go costs what was recorded and
    // never a walk over everything kept.
    private readonly Queue<(PivotRowKey? Row, string? Column, DateTimeOffset At)> _recorded = new();
    private HashSet<string> _valueColumns;

    private ReportHistory(PivotReport report, Func<DateTimeOffset> now, Func<TimeSpan> duration, bool canMark)
    {
        _now = now;
        _duration = duration;
        _valueColumns = NamesOf(report);
        // Made once per history: its identity is what the grid compares.
        Answer = canMark ? ChangedAt : null;
    }

    /// <summary>What the grid is handed: null when nothing can be marked — a duration of zero — so the
    /// grid asks nothing (DC-1).</summary>
    public CellChangeOf<PivotReportRow>? Answer { get; }

    /// <summary>How many rows and columns have a time kept, for layer 2.</summary>
    internal int Kept => _rows.Count + _columns.Count;

    /// <summary>A history of the report a change other than data laid out, which marks nothing.</summary>
    /// <param name="report">The report on screen.</param>
    /// <param name="now">The clock ExPivot reads.</param>
    /// <param name="duration">How long a mark lasts, read when asked.</param>
    public static ReportHistory Start(PivotReport report, Func<DateTimeOffset> now, Func<TimeSpan> duration)
        => new(report, now, duration, canMark: duration() > TimeSpan.Zero);

    /// <summary>
    /// The next data version, laid out under the history's layout and words: what changed in its
    /// painted values since the report before it, marked at <paramref name="at"/> — when its answer
    /// arrived. The rows and columns that left take their times with them, and the times whose marks
    /// have ended are let go.
    /// </summary>
    public void Record(PivotReport report, PivotReportChanges changes, DateTimeOffset at)
    {
        foreach (var key in changes.LeftRows)
            _rows.Remove(key);
        foreach (var name in changes.LeftColumns)
            _columns.Remove(name);
        foreach (var row in changes.NewRows)
        {
            var key = report.Rows[row].Key;
            TimesOf(key).Appeared(at);
            _recorded.Enqueue((key, null, at));
        }
        foreach (var column in changes.NewColumns)
        {
            var name = report.ValueColumns[column].Name;
            _columns[name] = at;
            _recorded.Enqueue((null, name, at));
        }
        foreach (var (row, column) in changes.Cells)
        {
            var key = report.Rows[row].Key;
            TimesOf(key).Changed(report.ValueColumns[column].Name, at);
            _recorded.Enqueue((key, null, at));
        }
        if (changes.NewColumns.Count > 0 || changes.LeftColumns.Count > 0)
            _valueColumns = NamesOf(report);
        LetGo();
    }

    // The times of a row, kept under the newest key recorded for it: an equal key of an earlier
    // report would hold that report's axis tree for as long as the row keeps changing.
    private RowTimes TimesOf(PivotRowKey key)
    {
        if (!_rows.Remove(key, out var times))
            times = new RowTimes();
        _rows.Add(key, times);
        return times;
    }

    private void LetGo()
    {
        var now = _now();
        var duration = _duration();
        while (_recorded.TryPeek(out var oldest) && ChangeHighlightRules.EndOf(oldest.At, duration) <= now)
        {
            _recorded.Dequeue();
            if (oldest.Row is { } key)
            {
                if (_rows.TryGetValue(key, out var times) && ChangeHighlightRules.EndOf(times.Newest, duration) <= now)
                    _rows.Remove(key);
            }
            else if (oldest.Column is { } name && _columns.TryGetValue(name, out var appeared)
                && ChangeHighlightRules.EndOf(appeared, duration) <= now)
            {
                _columns.Remove(name);
            }
        }
    }

    /// <summary>
    /// When the painted text of the cell at (<paramref name="row"/>, <paramref name="column"/>) last
    /// changed with the data, or null (ADR-0068): asked by the grid for each painted value cell of a
    /// row that renders. Null for a label cell, and once the mark has ended.
    /// </summary>
    private DateTimeOffset? ChangedAt(PivotReportRow row, GridColumn<PivotReportRow> column)
    {
        var name = column.Name;
        if (!_valueColumns.Contains(name))
            return null;
        DateTimeOffset? at = _columns.TryGetValue(name, out var appeared) ? appeared : null;
        if (_rows.TryGetValue(row.Key, out var times) && times.At(name) is { } changed && (at is null || changed > at))
            at = changed;
        return at is { } time && ChangeHighlightRules.EndOf(time, _duration()) > _now() ? time : null;
    }

    private static HashSet<string> NamesOf(PivotReport report)
        => report.ValueColumns.Select(column => column.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>One row's change times: when it appeared, if it did, and when each value column whose
    /// text changed last changed. A row changes a few cells at a time, so a short list.</summary>
    private sealed class RowTimes
    {
        private DateTimeOffset? _appeared;
        private List<(string Column, DateTimeOffset At)>? _columns;

        public DateTimeOffset Newest { get; private set; }

        public void Appeared(DateTimeOffset at)
        {
            _appeared = at;
            _columns = null;
            Newest = at;
        }

        public void Changed(string column, DateTimeOffset at)
        {
            _columns ??= [];
            Newest = at;
            for (var i = 0; i < _columns.Count; i++)
            {
                if (string.Equals(_columns[i].Column, column, StringComparison.Ordinal))
                {
                    _columns[i] = (column, at);
                    return;
                }
            }
            _columns.Add((column, at));
        }

        public DateTimeOffset? At(string column)
        {
            var at = _appeared;
            if (_columns is not null)
            {
                foreach (var (name, time) in _columns)
                {
                    if (string.Equals(name, column, StringComparison.Ordinal) && (at is null || time > at))
                        at = time;
                }
            }
            return at;
        }
    }
}
