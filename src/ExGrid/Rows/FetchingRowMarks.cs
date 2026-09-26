namespace ExGrid.Rows;

/// <summary>
/// The Row Marks of a <c>GridSource.Fetch</c> source (ADR-0043), kept as the steps that
/// made them — keys and snapshots, never positions — and evaluated per painted row through
/// the Consumer's <see cref="RowMarkAdapter{TRow}"/>. The counts come from the server; until
/// it has answered they are unknown, and the grid claims nothing from them.
/// </summary>
public sealed class FetchingRowMarks<TRow> : IRowMarks<TRow>
{
    private readonly FetchingGridSource<TRow> _source;
    private readonly RowMarkAdapter<TRow> _adapter;
    // The steps in the order they were taken, with each key's one step findable at once:
    // a Space over half a million rows is half a million steps, and neither adding one nor
    // asking a painted row may walk them all.
    private readonly LinkedList<(long Order, RowMarkStep Step)> _steps = new();
    private readonly Dictionary<object, LinkedListNode<(long Order, RowMarkStep Step)>> _keys = [];
    private readonly List<(long Order, RowMarkStep.AllOf Step)> _snapshots = [];
    private long _order;
    private Func<TRow, RowKind>? _rowKind;
    private RowMarkState _state = RowMarkState.Empty;
    private RowMarkCounts? _counts;
    private int _countGeneration;

    internal FetchingRowMarks(FetchingGridSource<TRow> source, RowMarkAdapter<TRow> adapter)
    {
        _source = source;
        _adapter = adapter;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>The marks as the steps that made them, oldest first — what the Consumer
    /// sends to its server to run an action over them (ADR-0043).</summary>
    public RowMarkState State => _state;

    /// <inheritdoc />
    public RowMarkCounts? Counts => _counts;

    /// <inheritdoc />
    public bool IsMarked(TRow row)
    {
        if (row is null || _steps.Count == 0 || !IsDetail(row))
            return false;
        // RowMarkState.IsMarked's rule — the latest step covering the row decides — made
        // without walking the keys: the row's own step is looked up, and only snapshots
        // taken after it can override it.
        var (order, marked) = _keys.TryGetValue(_adapter.Key(row), out var node)
            ? (node.Value.Order, node.Value.Step.Marked)
            : (-1L, false);
        for (var i = _snapshots.Count - 1; i >= 0 && _snapshots[i].Order > order; i--)
        {
            if (_adapter.BelongsTo(row, _snapshots[i].Step.Snapshot))
                return _snapshots[i].Step.Marked;
        }
        return marked;
    }

    /// <inheritdoc />
    public void OnRowKindChanged(Func<TRow, RowKind>? rowKind) => _rowKind = rowKind;

    /// <inheritdoc />
    public async Task OnMarkIntentAsync(RowMarkIntent<TRow> intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent is not (RowMarkIntent<TRow>.OneRow or RowMarkIntent<TRow>.AllRows or RowMarkIntent<TRow>.Positions))
            throw new ArgumentOutOfRangeException(nameof(intent), intent, "An unknown Row Mark intent.");

        // What fails from here on is the Consumer's server — the snapshot, a fetch past the
        // Window, the count — and it is reported the way a failed fetch is (ADR-0025): to
        // FetchFailed when someone listens, rather than thrown into the click or the key
        // that caused it, where on a circuit it would end the session. Whatever its type.
        try
        {
            var changed = intent switch
            {
                RowMarkIntent<TRow>.OneRow one => MarkOne(one.Row, one.Marked),
                RowMarkIntent<TRow>.AllRows all => await MarkResultAsync(all.Marked, all.RowSequenceVersion).ConfigureAwait(true),
                RowMarkIntent<TRow>.Positions positions => await MarkPositionsAsync(positions.Ranges, positions.RowSequenceVersion).ConfigureAwait(true),
                _ => false,
            };
            if (!changed)
                return;
            _state = new RowMarkState(_steps.Select(entry => entry.Step).ToArray());
            Changed?.Invoke();
            await RecountAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Reported from the catch body, not a filter: a handler that throws inside a
            // filter is swallowed by the runtime, and the original would be thrown as well.
            if (!_source.TryReportFailure(ex))
                throw;
        }
    }

    /// <summary>
    /// Asks the server for the counts again: after the marks change, the Filter changes,
    /// or the result grows — and whenever the Consumer knows its data moved, since a row
    /// arriving on the server is news only the server has. The counts are unknown until the
    /// answer lands; an answer overtaken by a newer question is discarded.
    /// </summary>
    public async Task RecountAsync()
    {
        var generation = ++_countGeneration;
        if (_counts is not null)
        {
            _counts = null;
            Changed?.Invoke();
        }
        var counts = await _adapter.Count(_state, _source.Filter, CancellationToken.None).ConfigureAwait(true);
        if (generation != _countGeneration)
            return;
        _counts = counts;
        Changed?.Invoke();
    }

    private bool MarkOne(TRow row, bool marked)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsDetail(row))
            return false;
        Append(new RowMarkStep.OneKey(_adapter.Key(row), marked));
        return true;
    }

    private async Task<bool> MarkResultAsync(bool marked, int version)
    {
        // "All" is the result the header was pressed over (ADR-0043): under another order
        // or filter it is not what the user saw — refused, and refused again if the
        // Filter moved while the server was opening the snapshot.
        if (version != _source.RowSequenceVersion)
            return false;
        var filter = _source.Filter;
        var asOf = await _adapter.OpenSnapshot(filter, CancellationToken.None).ConfigureAwait(true);
        if (version != _source.RowSequenceVersion)
            return false;
        Append(new RowMarkStep.AllOf(new RowMarkSnapshot(filter, asOf), marked));
        return true;
    }

    private async Task<bool> MarkPositionsAsync(IReadOnlyList<RowRange> ranges, int version)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (version != _source.RowSequenceVersion)
            return false;

        // Past the Window the rows are fetched, as a copy beyond it fetches them
        // (ADR-0005): positions become keys here, under the order they were taken in.
        var rows = new List<TRow>();
        foreach (var range in ranges)
        {
            var start = _source.WindowStart;
            var window = _source.Window;
            if (range.Start >= start && range.Start + range.Count <= start + window.Count)
            {
                for (var i = range.Start; i < range.Start + range.Count; i++)
                    rows.Add(window[i - start]);
            }
            else
            {
                rows.AddRange(await _source.GetRowsAsync(range, CancellationToken.None).ConfigureAwait(true));
            }
            if (version != _source.RowSequenceVersion)
                return false;
        }

        var detail = rows.Where(row => row is not null && IsDetail(row)).ToList();
        if (detail.Count == 0)
            return false;
        var target = RowMarkRules.LineUp(detail.Count(IsMarked), detail.Count);
        foreach (var row in detail)
            Append(new RowMarkStep.OneKey(_adapter.Key(row), target));
        return true;
    }

    /// <summary>Adds a step as the latest; a key keeps only its latest step, so a row
    /// toggled a thousand times is one step, not a thousand.</summary>
    private void Append(RowMarkStep step)
    {
        var order = ++_order;
        switch (step)
        {
            case RowMarkStep.OneKey one:
                if (_keys.Remove(one.Key, out var previous))
                    _steps.Remove(previous);
                _keys[one.Key] = _steps.AddLast((order, step));
                break;
            case RowMarkStep.AllOf all:
                _steps.AddLast((order, step));
                _snapshots.Add((order, all));
                break;
        }
    }

    private bool IsDetail(TRow row) => _rowKind is null || _rowKind(row) == RowKind.Detail;
}
