using ExGrid.Cells;

namespace ExGrid;

/// <summary>
/// A bundled source's Change Highlight (ADR-0141, on ADR-0067's rules and ADR-0068's
/// declaration): when each cell's painted text last changed, by Row Key and column name, and the
/// one <see cref="CellChangeOf{TRow}"/> the Consumer hands the grid. The source records here as it
/// pairs a row's new version with the one it had; the grid asks here as it paints.
///
/// <para><b>One delegate for the source's life.</b> Its identity is the change signal (ADR-0068): a
/// row whose instance changed renders and asks again, and no other row has anything new to ask,
/// so the delegate never has to change.</para>
///
/// <para><b>What is kept, and for how long.</b> Memory is bounded on two sides. A key the source
/// removes takes its times with it. And a time older than <see cref="KeptFor"/> is let go: a
/// fetching source never learns that a row it painted was removed — it only stops painting it —
/// and an in-memory source with a million rows that each change once a day would otherwise keep a
/// time for every one. What is let go is decided by data, never by when a render happens to ask:
/// the times are let go only as new ones are recorded, and the delegate answers null for a time
/// older than <see cref="KeptFor"/> whether or not it has been let go yet, so a mark never depends
/// on when it was asked for. A mark that should outlast <see cref="KeptFor"/> needs it raised: set
/// it to at least the grid's <c>ChangeHighlightDuration</c>.</para>
///
/// <para>Nothing is recorded until the delegate has been read: a source whose Consumer never asked
/// for the Change Highlight compares nothing (ADR-0068: without the declaration, nothing
/// changes).</para>
/// </summary>
internal sealed class CellChangeTimes<TRow>
{
    /// <summary>How long a change time is kept unless the Consumer says otherwise: the longest
    /// mark the demo pages offer, and sixty times the grid's default duration.</summary>
    public static readonly TimeSpan DefaultKeptFor = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Func<TRow, object> _key;
    private readonly TimeProvider _clock;
    private readonly Dictionary<object, KeyTimes> _times = new();
    // Every time recorded, oldest first, so that letting the old ones go costs what was recorded
    // and never a walk over everything kept.
    private readonly Queue<(object Key, DateTimeOffset At)> _recorded = new();
    private TimeSpan _keptFor = DefaultKeptFor;
    private volatile bool _wanted;

    public CellChangeTimes(Func<TRow, object> key, TimeProvider clock)
    {
        _key = key;
        _clock = clock;
        _lookup = Lookup;
    }

    private readonly CellChangeOf<TRow> _lookup;

    /// <summary>The delegate the Consumer hands the grid. Reading it starts the recording.</summary>
    public CellChangeOf<TRow> Delegate
    {
        get
        {
            _wanted = true;
            return _lookup;
        }
    }

    /// <summary>Whether anyone has asked for the delegate, and so whether pairing is worth the
    /// comparison.</summary>
    public bool Wanted => _wanted;

    /// <summary>How long a change time answers. Refused when not positive: a time kept for no time
    /// would mark nothing.</summary>
    public TimeSpan KeptFor
    {
        get
        {
            lock (_gate)
                return _keptFor;
        }
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "A change time must be kept for some time, or no cell is ever marked (ADR-0141/0068).");
            }
            lock (_gate)
                _keptFor = value;
        }
    }

    /// <summary>
    /// Records what one pairing found: for each row of <paramref name="pairs"/>, the columns of
    /// <paramref name="columns"/> whose painted text differs between the version the grid had and
    /// the new one; for each row of <paramref name="appeared"/>, every column; and the keys of
    /// <paramref name="removed"/> forgotten. The text compared is <see cref="ColumnInfo{TRow}.TextOf"/>,
    /// what the row paints by, so a change the format hides is not marked (ADR-0067).
    /// </summary>
    public void Record(
        IReadOnlyList<ColumnInfo<TRow>> columns,
        IReadOnlyList<(object Key, TRow Old, TRow New)> pairs,
        IReadOnlyList<object> appeared,
        IReadOnlyList<object> removed)
    {
        if (!_wanted)
            return;
        var now = _clock.GetUtcNow();
        // Compared outside the lock: TextOf runs the Consumer's own formats, and the grid may be
        // asking meanwhile.
        List<(object Key, string Column)>? changed = null;
        foreach (var (key, old, @new) in pairs)
        {
            foreach (var column in columns)
            {
                // An Action Column paints no value (ADR-0020), and the grid asks only of value cells.
                if (!column.IsQueryable)
                    continue;
                if (!string.Equals(column.TextOf(old), column.TextOf(@new), StringComparison.Ordinal))
                    (changed ??= []).Add((key, column.Name));
            }
        }
        lock (_gate)
        {
            foreach (var key in removed)
                _times.Remove(key);
            if (changed is not null)
            {
                foreach (var (key, column) in changed)
                {
                    TimesOf(key).Changed(column, now);
                    _recorded.Enqueue((key, now));
                }
            }
            foreach (var key in appeared)
            {
                TimesOf(key).Appeared(now);
                _recorded.Enqueue((key, now));
            }
            LetGo(now);
        }
    }

    private KeyTimes TimesOf(object key)
    {
        if (!_times.TryGetValue(key, out var times))
            _times.Add(key, times = new KeyTimes());
        return times;
    }

    private void LetGo(DateTimeOffset now)
    {
        var horizon = now - _keptFor;
        while (_recorded.TryPeek(out var oldest) && oldest.At < horizon)
        {
            _recorded.Dequeue();
            if (_times.TryGetValue(oldest.Key, out var times) && times.Newest < horizon)
                _times.Remove(oldest.Key);
        }
    }

    private DateTimeOffset? Lookup(TRow row, GridColumn<TRow> column)
    {
        if (row is null)
            return null;
        var key = _key(row);
        if (key is null)
            return null;
        DateTimeOffset? at;
        TimeSpan keptFor;
        lock (_gate)
        {
            if (!_times.TryGetValue(key, out var times))
                return null;
            at = times.At(column.Name);
            keptFor = _keptFor;
        }
        // A time older than the horizon answers null whether or not it has been let go yet, so
        // what the grid paints never depends on when the letting go happened.
        return at is { } time && _clock.GetUtcNow() - time <= keptFor ? time : null;
    }

    /// <summary>One row's change times: when it appeared, if it did, and when each column whose
    /// text changed last changed. A row changes a few cells at a time, so a short list.</summary>
    private sealed class KeyTimes
    {
        private DateTimeOffset? _appeared;
        private List<(string Column, DateTimeOffset At)>? _columns;

        public DateTimeOffset Newest { get; private set; }

        public void Appeared(DateTimeOffset at)
        {
            _appeared = at;
            Newest = at;
        }

        public void Changed(string column, DateTimeOffset at)
        {
            _columns ??= [];
            for (var i = 0; i < _columns.Count; i++)
            {
                if (string.Equals(_columns[i].Column, column, StringComparison.Ordinal))
                {
                    _columns[i] = (column, at);
                    Newest = at;
                    return;
                }
            }
            _columns.Add((column, at));
            Newest = at;
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
