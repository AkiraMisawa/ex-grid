namespace ExGrid.Rows;

/// <summary>
/// The Row Marks of a <c>GridSource.From</c> source (ADR-0043). Everything is in hand, so
/// the marks are kept per row of the base — by identity, which the source never loses: a
/// value update goes through <see cref="InMemoryGridSource{TRow}.ReplaceRow"/>, and the
/// mark moves to the new instance with it.
///
/// <para>"All" is taken as the result stands at the press: every Detail row in it is
/// marked there and then, so a row that joins the result afterwards — an edit that now
/// matches the filter — is not.</para>
/// </summary>
public sealed class InMemoryRowMarks<TRow> : IRowMarks<TRow>
{
    private readonly InMemoryGridSource<TRow> _source;
    private readonly IReadOnlyList<TRow> _rows;
    private readonly Dictionary<object, int> _slots;
    private readonly bool[] _marked;
    private Func<TRow, RowKind>? _rowKind;

    // The counts walk the whole result, so they are kept until what they are made of moves:
    // the Window (a new list on every requery), the marks, or the roles.
    private IReadOnlyList<TRow>? _countedWindow;
    private RowMarkCounts _counts;
    private bool _countsStale = true;

    internal InMemoryRowMarks(InMemoryGridSource<TRow> source, IReadOnlyList<TRow> rows)
    {
        _source = source;
        _rows = rows;
        _marked = new bool[rows.Count];
        _slots = new Dictionary<object, int>(rows.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < rows.Count; i++)
            if (rows[i] is { } row)
                _slots.TryAdd(row, i);
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public bool IsMarked(TRow row)
        => row is not null && _slots.TryGetValue(row, out var slot) && _marked[slot] && IsDetail(row);

    /// <inheritdoc />
    public RowMarkCounts? Counts
    {
        get
        {
            var window = _source.Window;
            if (_countsStale || !ReferenceEquals(_countedWindow, window))
            {
                _counts = Count(window);
                _countedWindow = window;
                _countsStale = false;
            }
            return _counts;
        }
    }

    /// <summary>The marked rows, as the instances the source holds now, in the order the
    /// rows were handed to the source — what an action runs over. Rows, never positions
    /// (ADR-0043): positions name other rows after a sort.</summary>
    public IReadOnlyList<TRow> MarkedRows
    {
        get
        {
            var marked = new List<TRow>();
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_marked[i] && IsDetail(_rows[i]))
                    marked.Add(_rows[i]);
            }
            return marked;
        }
    }

    /// <inheritdoc />
    public void OnRowKindChanged(Func<TRow, RowKind>? rowKind)
    {
        if (ReferenceEquals(rowKind, _rowKind))
            return;
        _rowKind = rowKind;
        _countsStale = true;
    }

    /// <inheritdoc />
    public Task OnMarkIntentAsync(RowMarkIntent<TRow> intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var changed = intent switch
        {
            RowMarkIntent<TRow>.OneRow one => MarkOne(one.Row, one.Marked),
            RowMarkIntent<TRow>.AllRows all => MarkResult(all.Marked, all.RowSequenceVersion),
            RowMarkIntent<TRow>.Positions positions => MarkPositions(positions.Ranges, positions.RowSequenceVersion),
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "An unknown Row Mark intent."),
        };
        if (changed)
        {
            _countsStale = true;
            Changed?.Invoke();
        }
        return Task.CompletedTask;
    }

    /// <summary>The source replaced a row by a new instance: the mark belongs to the row,
    /// so it moves with it.</summary>
    internal void Replaced(TRow row, TRow replacement)
    {
        if (row is null || replacement is null || !_slots.Remove(row, out var slot))
            return;
        _slots[replacement] = slot;
        _countsStale = true;
    }

    private bool MarkOne(TRow row, bool marked)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!_slots.TryGetValue(row, out var slot) || !IsDetail(row))
            return false;
        return Set(slot, marked);
    }

    private bool MarkResult(bool marked, int version)
    {
        // "All" is the result the header was pressed over. A filter change since then
        // moved the version, and the current result is not the one the user saw: refused,
        // as a positional gesture is.
        if (version != _source.RowSequenceVersion)
            return false;
        var changed = false;
        foreach (var row in _source.Window)
        {
            if (row is not null && IsDetail(row) && _slots.TryGetValue(row, out var slot))
                changed |= Set(slot, marked);
        }
        return changed;
    }

    private bool MarkPositions(IReadOnlyList<RowRange> ranges, int version)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        // Positions taken under another order name other rows: refused, and nothing is
        // marked (ADR-0043, as Held Selection refuses a selection across a reorder).
        if (version != _source.RowSequenceVersion)
            return false;

        var window = _source.Window;
        var slots = new List<int>();
        var markedNow = 0;
        foreach (var range in ranges)
        {
            var end = Math.Min(range.Start + range.Count, window.Count);
            for (var i = range.Start; i < end; i++)
            {
                var row = window[i];
                if (row is null || !IsDetail(row) || !_slots.TryGetValue(row, out var slot))
                    continue;
                slots.Add(slot);
                if (_marked[slot])
                    markedNow++;
            }
        }
        if (slots.Count == 0)
            return false;

        var target = RowMarkRules.LineUp(markedNow, slots.Count);
        var changed = false;
        foreach (var slot in slots)
            changed |= Set(slot, target);
        return changed;
    }

    private bool Set(int slot, bool marked)
    {
        if (_marked[slot] == marked)
            return false;
        _marked[slot] = marked;
        return true;
    }

    private bool IsDetail(TRow row) => _rowKind is null || _rowKind(row) == RowKind.Detail;

    private RowMarkCounts Count(IReadOnlyList<TRow> window)
    {
        var inResult = 0;
        var rowsInResult = 0;
        foreach (var row in window)
        {
            if (row is null || !IsDetail(row))
                continue;
            rowsInResult++;
            if (_marked[_slots[row]])
                inResult++;
        }
        var total = 0;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_marked[i] && IsDetail(_rows[i]))
                total++;
        }
        return new RowMarkCounts(inResult, rowsInResult, total - inResult);
    }
}
