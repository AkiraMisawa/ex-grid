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
    private readonly List<RowMarkStep> _steps = [];
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
        return _state.IsMarked(_adapter.Key(row), snapshot => _adapter.BelongsTo(row, snapshot));
    }

    /// <inheritdoc />
    public void OnRowKindChanged(Func<TRow, RowKind>? rowKind) => _rowKind = rowKind;

    /// <inheritdoc />
    public async Task OnMarkIntentAsync(RowMarkIntent<TRow> intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var changed = intent switch
        {
            RowMarkIntent<TRow>.OneRow one => MarkOne(one.Row, one.Marked),
            RowMarkIntent<TRow>.AllRows all => await MarkResultAsync(all.Marked, all.RowSequenceVersion).ConfigureAwait(true),
            RowMarkIntent<TRow>.Positions positions => await MarkPositionsAsync(positions.Ranges, positions.RowSequenceVersion).ConfigureAwait(true),
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "An unknown Row Mark intent."),
        };
        if (!changed)
            return;
        _state = new RowMarkState(_steps.ToArray());
        Changed?.Invoke();
        await RecountAsync().ConfigureAwait(true);
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
        if (step is RowMarkStep.OneKey one)
            _steps.RemoveAll(existing => existing is RowMarkStep.OneKey old && old.Key.Equals(one.Key));
        _steps.Add(step);
    }

    private bool IsDetail(TRow row) => _rowKind is null || _rowKind(row) == RowKind.Detail;
}
