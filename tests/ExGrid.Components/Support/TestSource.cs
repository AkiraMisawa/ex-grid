namespace ExGrid.Components.Tests.Support;

/// <summary>
/// A Grid Source the test drives by hand: it holds whatever state is set on it and
/// raises <see cref="StateChanged"/> when told to. The in-memory source is the reference
/// implementation and is used where its real behaviour is the point; this one exists for
/// the cases it cannot reach — a Window that moves, a load in flight, a Range Request
/// nobody answers.
/// </summary>
internal sealed class TestSource : IGridSource<TestRow>
{
    public IReadOnlyList<TestRow> Window { get; private set; } = [];

    public int WindowStart { get; private set; }

    public int? TotalCount { get; private set; }

    public bool IsLoading { get; private set; }

    public int RowSequenceVersion { get; private set; }

    public List<RowRange> Requested { get; } = [];

    public IReadOnlyList<ColumnInfo<TestRow>>? Columns { get; private set; }

    public int ColumnPushes { get; private set; }

    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    public GridFilter? Filter { get; private set; }

    /// <summary>Every Sorts list the grid has handed over, so a test can assert the
    /// gesture without the source deciding anything about it.</summary>
    public List<IReadOnlyList<SortSpec>> SortChanges { get; } = [];

    public event Action? StateChanged;

    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TestRow>> columns)
    {
        Columns = columns;
        ColumnPushes++;
    }

    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        Sorts = sorts;
        SortChanges.Add(sorts);
    }

    public void OnFilterChanged(GridFilter? filter)
    {
        Filter = filter;
        FilterChanges.Add(filter);
    }

    public List<GridFilter?> FilterChanges { get; } = [];

    /// <summary>What the value list answers; every request is recorded (ADR-0009).</summary>
    public Chrome.DistinctValues DistinctAnswer { get; set; } = Chrome.DistinctValues.Of([]);

    public List<string> DistinctRequested { get; } = [];

    /// <summary>When set, the value list answers only once the test completes it — a
    /// Consumer whose query is still running.</summary>
    public TaskCompletionSource<Chrome.DistinctValues>? DistinctPending { get; set; }

    public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        DistinctRequested.Add(column);
        return DistinctPending?.Task ?? Task.FromResult(DistinctAnswer);
    }

    public Task OnRangeNeededAsync(RowRange range)
    {
        Requested.Add(range);
        return Task.CompletedTask;
    }

    /// <summary>What a copy beyond the Window is answered with; every request is
    /// recorded. Defaults to the slice of the Window by absolute position.</summary>
    public Func<RowRange, IReadOnlyList<TestRow>>? CopyRows { get; set; }

    public List<RowRange> CopyRequested { get; } = [];

    public Task<IReadOnlyList<TestRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        CopyRequested.Add(range);
        if (CopyRows is not null)
            return Task.FromResult(CopyRows(range));
        var start = Math.Max(range.Start, WindowStart);
        var end = Math.Min(range.Start + range.Count, WindowStart + Window.Count);
        var rows = new List<TestRow>();
        for (var i = start; i < end; i++)
            rows.Add(Window[i - WindowStart]);
        return Task.FromResult<IReadOnlyList<TestRow>>(rows);
    }

    /// <summary>Moves the state and notifies, the way a real source does when an answer
    /// lands.</summary>
    public void Push(
        IReadOnlyList<TestRow> window,
        int windowStart = 0,
        int? totalCount = null,
        bool isLoading = false,
        int? rowSequenceVersion = null)
    {
        Window = window;
        WindowStart = windowStart;
        TotalCount = totalCount;
        IsLoading = isLoading;
        RowSequenceVersion = rowSequenceVersion ?? RowSequenceVersion;
        Raise();
    }

    public void Raise() => StateChanged?.Invoke();
}
