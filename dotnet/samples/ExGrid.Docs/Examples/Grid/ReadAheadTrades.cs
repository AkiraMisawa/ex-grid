using ExGrid.Chrome;
using ExGrid.Docs.Examples.Data;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>
/// The million trades as a server would send them, a Window at a time, for a grid that may ask
/// before its Viewport runs out (ADR-0183): a Grid Source of the page's own. Each question is
/// answered after a round trip with the rows asked for and <see cref="ReadAhead"/> either side. A
/// question in flight whose rows cover the rows on screen is kept rather than asked again
/// (ADR-0182): replacing it at every scroll would restart the round trip for as long as a key is
/// held. It counts the questions, and how many of them found every row on screen already held. A
/// question asked early is asked quietly: the grid is dimmed only while rows on screen wait for an
/// answer.
/// </summary>
public sealed class ReadAheadTrades : IGridSource<Trade>
{
    /// <summary>Rows read either side of the rows asked for.</summary>
    public const int ReadAhead = 50;

    /// <summary>How far short of the Window's edge the grid asks, when it asks early: never more
    /// than <see cref="ReadAhead"/>, or the answer would not reach it and the next row would ask
    /// again.</summary>
    public const int Margin = 10;

    private static readonly TimeSpan RoundTrip = TimeSpan.FromMilliseconds(150);

    // The rows asked of the server and not yet answered, if any.
    private (int Start, int Count)? _asked;

    /// <summary>The rows held, which the grid paints.</summary>
    public IReadOnlyList<Trade> Window { get; private set; } = MillionTrades.Range(0, 100);

    /// <summary>Where <see cref="Window"/> starts among the million.</summary>
    public int WindowStart { get; private set; }

    /// <inheritdoc />
    public int? TotalCount => MillionTrades.Count;

    /// <summary>Whether rows on screen wait for the question out: the grid's loading indication.
    /// A question asked while every row on screen is held raises none, or the grid would dim at
    /// every Window's edge with nothing on screen missing.</summary>
    public bool IsLoading { get; private set; }

    /// <inheritdoc />
    public int RowSequenceVersion => 0;

    /// <inheritdoc />
    public IReadOnlyList<SortSpec> Sorts => [];

    /// <inheritdoc />
    public GridFilter? Filter => null;

    /// <summary>A trade's id names it across Windows.</summary>
    public Func<Trade, object> RowKey { get; } = static t => t.Id;

    /// <summary>A trade's id is never repeated, so the grid need not check.</summary>
    public bool VouchesDistinctRows => true;

    /// <summary>Questions asked while every row on screen was held: asked early.</summary>
    public int Early { get; private set; }

    /// <summary>Questions asked while rows on screen waited for them as placeholders.</summary>
    public int Late { get; private set; }

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <summary>Starts the counts again.</summary>
    public void ResetCounts()
    {
        (Early, Late) = (0, 0);
        StateChanged?.Invoke();
    }

    // This server neither sorts nor filters, and no column is queryable: the grid never asks.

    /// <inheritdoc />
    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        if (sorts.Count > 0)
            throw new NotSupportedException("This server does not sort.");
    }

    /// <inheritdoc />
    public void OnFilterChanged(GridFilter? filter)
    {
        if (filter is not null)
            throw new NotSupportedException("This server does not filter.");
    }

    /// <inheritdoc />
    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<Trade>> columns)
    {
        // The columns name nothing this server reads.
    }

    /// <summary>The grid's Range Request: it names the rows on screen, early or not.</summary>
    public async Task OnRangeNeededAsync(RowRange range)
    {
        var held = Covers(WindowStart, Window.Count, range);
        if (_asked is { } inFlight && Covers(inFlight.Start, inFlight.Count, range))
        {
            // Kept; but once the rows on screen leave the rows held, they wait for it.
            if (!held && !IsLoading)
            {
                IsLoading = true;
                StateChanged?.Invoke();
            }
            return;
        }
        if (held)
            Early++;
        else
            Late++;
        IsLoading = !held;
        var start = Math.Max(0, range.Start - ReadAhead);
        var end = Math.Min(MillionTrades.Count, range.Start + range.Count + ReadAhead);
        var asked = (start, end - start);
        _asked = asked;
        StateChanged?.Invoke();
        await Task.Delay(RoundTrip); // where a server call would go

        // A newer question replaced this one while it waited: its answer would be stale.
        if (_asked != asked)
            return;
        Window = MillionTrades.Range(start, end - start);
        WindowStart = start;
        _asked = null;
        IsLoading = false;
        StateChanged?.Invoke();
    }

    /// <summary>The rows of a copy that reaches past the Window: asked of the server on their own.</summary>
    public async Task<IReadOnlyList<Trade>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        await Task.Delay(RoundTrip, cancellationToken);
        return MillionTrades.Range(range.Start, Math.Min(range.Count, MillionTrades.Count - range.Start));
    }

    /// <inheritdoc />
    public Task<DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
        => Task.FromResult(DistinctValues.TooMany);

    private static bool Covers(int start, int count, RowRange range)
        => range.Start >= start && range.Start + range.Count <= start + count;
}
