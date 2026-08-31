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

    public event Action? StateChanged;

    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TestRow>> columns)
    {
        Columns = columns;
        ColumnPushes++;
    }

    public Task OnRangeNeededAsync(RowRange range)
    {
        Requested.Add(range);
        return Task.CompletedTask;
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
