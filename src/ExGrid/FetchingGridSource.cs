using System.Runtime.ExceptionServices;

namespace ExGrid;

/// <summary>
/// <c>GridSource.Fetch</c>: the bundled Grid Source for a result that lives somewhere
/// else (ADR-0025). It holds one Window at a time, asks for the next one when the grid
/// says it needs rows, and takes on the three things ADR-0001 left to "the Consumer, or
/// GridSource" — read-ahead, coalescing while scrolling, and discarding an answer that
/// arrived after the question changed.
///
/// <para>It fetches the first page itself. The grid cannot ask for rows in a result it
/// has been told is empty — "nothing to show is not a range" (ADR-0001) — so a source
/// that starts with nothing has to break its own cold start.</para>
/// </summary>
public sealed class FetchingGridSource<TRow> : IGridSource<TRow>
{
    /// <summary>How many rows the first fetch asks for, before the grid has said how
    /// tall it is. Wide enough for an ordinary Viewport; the grid's own request replaces
    /// it as soon as one arrives.</summary>
    public const int DefaultPageRows = 100;

    private readonly Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> _fetch;
    private readonly int _readAheadRows;

    // Captured where the source is constructed — the Consumer's component, so Blazor's
    // dispatcher. It is where a failure nobody subscribed to is rethrown, since the task
    // that failed may be one nobody awaited (see StartDetached).
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    private CancellationTokenSource? _cancellation;
    private RowRange? _inFlight;
    private Task _current = Task.CompletedTask;
    private int _generation;
    private int _pageRows = DefaultPageRows;
    private bool _started;

    internal FetchingGridSource(
        Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
        int readAheadRows)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        ArgumentOutOfRangeException.ThrowIfNegative(readAheadRows);
        _fetch = fetch;
        _readAheadRows = readAheadRows;
    }

    public IReadOnlyList<TRow> Window { get; private set; } = [];

    public int WindowStart { get; private set; }

    /// <summary>Zero rather than null until the first answer: null would claim this empty
    /// Window <em>is</em> the whole result, and a later non-zero start would contradict
    /// it (ADR-0001).</summary>
    public int? TotalCount { get; private set; } = 0;

    public bool IsLoading { get; private set; }

    /// <summary>Bumped by a Sort or Filter change and by nothing else. Scrolling to
    /// another slice of the same query is not a reorder, and dropping the selection for
    /// it would make a long selection impossible to build (ADR-0011).</summary>
    public int RowSequenceVersion { get; private set; }

    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    public GridFilter? Filter { get; private set; }

    /// <summary>The last failure, kept so a Consumer can show it without subscribing.
    /// Cleared by the next answer that lands.</summary>
    public Exception? LastError { get; private set; }

    public event Action? StateChanged;

    /// <summary>
    /// Raised when a fetch fails. <b>With no subscriber the exception is rethrown</b> —
    /// on the awaiting grid when the grid asked, and otherwise on the synchronization
    /// context this source was created on, so it reaches the host's error path. A failed
    /// fetch that vanished would leave the previous rows on screen looking current, which
    /// is the one outcome this component refuses.
    /// </summary>
    public event Action<Exception>? FetchFailed;

    /// <summary>The columns are the grid's declaration, not part of the query: a server
    /// is asked for a range under a Sort and a Filter, and answers with rows. Bind time is
    /// what this is used for — it is when the first page is worth fetching.</summary>
    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (_started)
            return;
        _started = true;
        StartDetached(new RowRange(0, _pageRows));
    }

    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);
        if (sorts.SequenceEqual(Sorts))
            return;
        Sorts = sorts.ToArray();
        Restart();
    }

    public void OnFilterChanged(GridFilter? filter)
    {
        if (ReferenceEquals(filter, Filter))
            return;
        Filter = filter;
        Restart();
    }

    public Task OnRangeNeededAsync(RowRange range)
    {
        // The grid asks for what is on screen, so this is also how the source learns how
        // tall the Viewport is — what a restart should fetch to fill it.
        _pageRows = Math.Max(_pageRows, range.Count);
        _started = true;

        if (Covers(range))
            return Task.CompletedTask;

        var wanted = Expand(range);
        // The same range again while it is still in flight: hand back the fetch already
        // running rather than cancelling it and asking for exactly the same thing.
        // Awaiting it is also what carries a failure to the grid.
        if (_inFlight == wanted)
            return _current;

        return Start(wanted);
    }

    private bool Covers(RowRange range)
        => range.Start >= WindowStart && range.Start + range.Count <= WindowStart + Window.Count;

    private RowRange Expand(RowRange range)
    {
        var start = Math.Max(0, range.Start - _readAheadRows);
        var end = range.Start + range.Count + _readAheadRows;
        if (TotalCount is > 0 and var total)
            end = Math.Min(end, total);
        return new RowRange(start, Math.Max(1, end - start));
    }

    /// <summary>
    /// A Sort or Filter change invalidates everything: the rows, their order and the
    /// total, which the new query has not counted yet. The Window is dropped rather than
    /// left standing — under a new filter the old rows are simply the wrong ones — and
    /// the first page is fetched from the top, because with nothing on screen the grid
    /// has no range to ask for.
    /// </summary>
    private void Restart()
    {
        RowSequenceVersion++;
        Window = [];
        WindowStart = 0;
        TotalCount = 0;
        StartDetached(new RowRange(0, _pageRows));
    }

    private void StartDetached(RowRange range) => _ = SurfaceAsync(Start(range));

    private Task Start(RowRange range)
    {
        // The previous answer can no longer be the truth, so it is cancelled and, if it
        // arrives anyway, discarded by generation below. Cancelling is a courtesy to the
        // server; the generation check is what makes it correct.
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _inFlight = range;
        var generation = ++_generation;
        // Only when it actually flips: a second range asked for while the first is still
        // in flight moves nothing the grid paints, and an event for it would repaint
        // every row for nothing (ADR-0023's no-op principle).
        var wasLoading = IsLoading;
        IsLoading = true;
        if (!wasLoading)
            StateChanged?.Invoke();
        _current = RunAsync(range, generation, cancellation.Token);
        return _current;
    }

    private async Task RunAsync(RowRange range, int generation, CancellationToken cancellation)
    {
        try
        {
            var page = await _fetch(new GridQuery(range, Sorts, Filter), cancellation).ConfigureAwait(true);
            // Late and superseded: a newer question has been asked, and this answer is to
            // the old one. Applying it would put rows on screen that nothing asked for —
            // the failure ADR-0001 names when it hands stale-answer discarding to the
            // source.
            if (generation != _generation)
                return;
            ArgumentNullException.ThrowIfNull(page);
            Apply(range, page);
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
            // Our own cancellation, already superseded. Nothing to report.
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;
            _inFlight = null;
            IsLoading = false;
            LastError = ex;
            StateChanged?.Invoke();
            if (FetchFailed is { } handler)
            {
                handler(ex);
                return;
            }

            throw;
        }
    }

    private void Apply(RowRange asked, GridPage<TRow> page)
    {
        // A server free to clamp is not free to answer somewhere else: a page that does
        // not reach the row that was asked for leaves the Viewport on Placeholders, and
        // the grid asks again — for the same range, and gets the same answer. Refused by
        // name rather than spun on. Answering short because the result shrank past the
        // asked-for position is legitimate, and says so by its own total.
        if (page.Start > asked.Start
            || (asked.Start < page.TotalCount && page.Start + page.Rows.Count <= asked.Start))
        {
            throw new InvalidOperationException(
                $"Asked for {asked.Count} rows at {asked.Start} and got {page.Rows.Count} at {page.Start} " +
                $"of {page.TotalCount}, which does not cover the position asked for. A source may answer with " +
                "fewer rows, or with none when the result has shrunk past that position, but not with a " +
                "different part of the result (ADR-0025).");
        }

        _inFlight = null;
        IsLoading = false;
        LastError = null;
        Window = page.Rows;
        WindowStart = page.Start;
        TotalCount = page.TotalCount;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Watches a fetch nobody awaited — the cold start and a restart are the source's
    /// own. A failure there still has to reach the host rather than sit in an unobserved
    /// task, so with no <see cref="FetchFailed"/> subscriber it is rethrown on the
    /// context this source was created on. <see cref="LastError"/> holds it either way.
    /// </summary>
    private async Task SurfaceAsync(Task fetch)
    {
        try
        {
            await fetch.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _context?.Post(
                static state => ExceptionDispatchInfo.Capture((Exception)state!).Throw(),
                ex);
        }
    }
}
