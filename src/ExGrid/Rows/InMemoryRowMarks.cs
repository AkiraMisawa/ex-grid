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
    // An instance's first position in the base, and each position's next one holding the
    // same instance (-1 ends the chain). One instance handed over twice is one row by
    // identity (ADR-0003), so its positions share a mark; the chain is what lets a
    // replacement at one of them take the mark with it and leave the other its own.
    private readonly Dictionary<object, int> _slots;
    private readonly int[] _nextSame;
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
        _nextSame = new int[rows.Count];
        _slots = new Dictionary<object, int>(rows.Count, ReferenceEqualityComparer.Instance);
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            _nextSame[i] = -1;
            if (rows[i] is { } row)
                Link(row, i);
        }
    }

    /// <summary>Puts a position at the head of its instance's chain.</summary>
    private void Link(object row, int index)
    {
        _nextSame[index] = _slots.TryGetValue(row, out var first) ? first : -1;
        _slots[row] = index;
    }

    /// <summary>Takes a position out of its instance's chain.</summary>
    private void Unlink(object row, int index)
    {
        if (!_slots.TryGetValue(row, out var first))
            return;
        if (first == index)
        {
            if (_nextSame[index] < 0)
                _slots.Remove(row);
            else
                _slots[row] = _nextSame[index];
        }
        else
        {
            var at = first;
            while (_nextSame[at] >= 0 && _nextSame[at] != index)
                at = _nextSame[at];
            if (_nextSame[at] == index)
                _nextSame[at] = _nextSame[index];
        }
        _nextSame[index] = -1;
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
    /// (ADR-0043): positions name other rows after a sort. An instance handed over twice
    /// is one row, listed once.</summary>
    public IReadOnlyList<TRow> MarkedRows
    {
        get
        {
            var marked = new List<TRow>();
            var listed = new HashSet<object>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_marked[i] && _rows[i] is { } row && IsDetail(_rows[i]) && listed.Add(row))
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

    /// <summary>The source replaced the row at one position of its base by a new
    /// instance: the mark belongs to the row, so the replacement carries it. A copy of the
    /// old instance at another position keeps its own. Should the replacement already
    /// stand elsewhere, it is one row by identity, and takes the mark that moved.</summary>
    internal void Replaced(int index, TRow row, TRow replacement)
    {
        if (row is null || replacement is null)
            return;
        var marked = _marked[index];
        Unlink(row, index);
        Link(replacement, index);
        Set(_slots[replacement], marked);
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
        var slots = new HashSet<int>();
        var markedNow = 0;
        foreach (var range in ranges)
        {
            var end = Math.Min(range.Start + range.Count, window.Count);
            for (var i = range.Start; i < end; i++)
            {
                var row = window[i];
                if (row is null || !IsDetail(row) || !_slots.TryGetValue(row, out var slot) || !slots.Add(slot))
                    continue;
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

    /// <summary>Marks or unmarks every position of the instance whose chain starts at
    /// <paramref name="slot"/>. True when anything moved.</summary>
    private bool Set(int slot, bool marked)
    {
        var changed = false;
        for (var at = slot; at >= 0; at = _nextSame[at])
        {
            if (_marked[at] != marked)
            {
                _marked[at] = marked;
                changed = true;
            }
        }
        return changed;
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
            if (_slots.TryGetValue(row, out var slot) && _marked[slot])
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
