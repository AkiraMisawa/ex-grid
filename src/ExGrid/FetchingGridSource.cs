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
public sealed class FetchingGridSource<TRow> : IGridSource<TRow>, IDisposable, IBindsToOneCircuit
{
    CircuitBinding IBindsToOneCircuit.Binding { get; } = new();

    /// <summary>How many rows the first fetch asks for, before the grid has said how
    /// tall it is. Wide enough for an ordinary Viewport; the grid's own request replaces
    /// it as soon as one arrives.</summary>
    public const int DefaultPageRows = 100;

    private readonly Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> _fetch;
    private readonly int _readAheadRows;
    private readonly Func<string, GridFilter?, CancellationToken, Task<Chrome.DistinctValues>>? _distinctValues;

    // Captured where the source is constructed — the Consumer's component, so Blazor's
    // dispatcher. It is where a failure nobody subscribed to is rethrown, since the task
    // that failed may be one nobody awaited (see StartDetached).
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    private CancellationTokenSource? _cancellation;
    private RowRange? _inFlight;
    private bool _disposed;
    private Task _current = Task.CompletedTask;
    private int _generation;
    private int _pageRows = DefaultPageRows;
    private bool _started;

    internal FetchingGridSource(
        Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
        int readAheadRows,
        Func<string, GridFilter?, CancellationToken, Task<Chrome.DistinctValues>>? distinctValues = null,
        Rows.RowMarkAdapter<TRow>? marks = null)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        ArgumentOutOfRangeException.ThrowIfNegative(readAheadRows);
        _fetch = fetch;
        _readAheadRows = readAheadRows;
        _distinctValues = distinctValues;
        Marks = marks is null ? null : new Rows.FetchingRowMarks<TRow>(this, marks);
    }

    /// <summary>The Row Marks of this source, when it was given a
    /// <see cref="Rows.RowMarkAdapter{TRow}"/> — or null, and a Mark Column bound to it is
    /// refused by name (ADR-0043): the source receives new instances with every answer and
    /// cannot count beyond its Window, so without the Consumer's key and server it could
    /// only guess.</summary>
    public Rows.FetchingRowMarks<TRow>? Marks { get; }

    Rows.IRowMarks<TRow>? IGridSource<TRow>.Marks => Marks;

    /// <summary>The rows of the last answer that landed. Empty before the first answer,
    /// and again after a Sort or Filter change until the new query's first page lands.</summary>
    public IReadOnlyList<TRow> Window { get; private set; } = [];

    /// <summary>The position of <see cref="Window"/>[0] in the whole result, as the last
    /// answer reported it (ADR-0001).</summary>
    public int WindowStart { get; private set; }

    /// <summary>Zero rather than null until the first answer: null would claim this empty
    /// Window <em>is</em> the whole result, and a later non-zero start would contradict
    /// it (ADR-0001).</summary>
    public int? TotalCount { get; private set; } = 0;

    /// <summary>Whether a fetch is in flight: set when one starts, cleared when the
    /// answer the source is waiting for lands or fails (ADR-0001).</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Bumped by a Sort or Filter change, and by an answer whose total is smaller
    /// than the last one's — the positions then name different rows. Scrolling to another
    /// slice of the same query is not a reorder, and dropping the selection for it would
    /// make a long selection impossible to build (ADR-0011).</summary>
    public int RowSequenceVersion { get; private set; }

    /// <summary>The Sort in force, outermost first; empty for the server's own order. A
    /// copy of the list last handed to <see cref="OnSortChanged"/>.</summary>
    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    /// <summary>The Filter in force, or null for none — a structural copy of the one last
    /// handed to <see cref="OnFilterChanged"/>, so the Consumer's own collections behind
    /// it can change without reaching this (ADR-0023).</summary>
    public GridFilter? Filter { get; private set; }

    /// <summary>The last failure, kept so a Consumer can show it without subscribing.
    /// Cleared by the next answer that lands.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Raised when something the grid paints moved: a fetch started or failed,
    /// an answer landed, or a Sort or Filter change dropped the Window.</summary>
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
        if (_started || _disposed)
            return;
        _started = true;
        var first = new RowRange(0, _pageRows);
        StartDetached(first, first);
        Recount();
    }

    /// <summary>The counts depend on the Filter and on what the server holds; asked for
    /// again, detached, whenever either may have moved. A failure surfaces as a fetch's
    /// does, and the counts stay unknown rather than stale.</summary>
    private void Recount()
    {
        if (Marks is not null && !_disposed)
            _ = SurfaceAsync(RecountReportingAsync(Marks));
    }

    // A failed count is reported the way a failed fetch is: to FetchFailed when someone
    // listens, rethrown on the source's context when nobody does.
    private async Task RecountReportingAsync(Rows.FetchingRowMarks<TRow> marks)
    {
        try
        {
            await marks.RecountAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // In the catch body, never a filter: a throwing handler inside a filter is
            // swallowed by the runtime.
            if (!TryReportFailure(ex))
                throw;
        }
    }

    /// <summary>A failure of the Consumer's own server met while keeping the marks,
    /// reported as a failed fetch is: held in <see cref="LastError"/> and handed to
    /// <see cref="FetchFailed"/>. False when nobody listens, and the caller rethrows —
    /// the failure must reach the host rather than vanish (ADR-0025).</summary>
    internal bool TryReportFailure(Exception error)
    {
        if (_disposed || FetchFailed is not { } handler)
            return false;
        LastError = error;
        StateChanged?.Invoke();
        handler(error);
        return true;
    }

    /// <summary>Takes a new Sort. A list equal to the one in force is a no-op; any other
    /// drops the Window, bumps <see cref="RowSequenceVersion"/> and fetches the first page
    /// under the new query (ADR-0025).</summary>
    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);
        if (sorts.SequenceEqual(Sorts))
            return;
        Sorts = sorts.ToArray();
        Restart();
    }

    /// <summary>Takes a new Filter, compared structurally. An equal one is a no-op; any
    /// other drops the Window, bumps <see cref="RowSequenceVersion"/> and fetches the
    /// first page under the new query (ADR-0025).</summary>
    public void OnFilterChanged(GridFilter? filter)
    {
        // Structurally, and stored as a copy: a Consumer's GridFilter is typically backed
        // by its own live Dictionary and List, so comparing or keeping it by reference
        // makes a mutation look like nothing and a re-hand look like a change — both
        // wrong, and both silent (ADR-0023).
        if (GridFilters.Equal(filter, Filter))
            return;
        Filter = GridFilters.Snapshot(filter);
        Restart();
    }

    /// <summary>
    /// The grid needs these rows. A range the Window already covers asks nothing;
    /// otherwise the range widened by the read-ahead is fetched and supersedes whatever
    /// was in flight — unless it is that same range, whose fetch is handed back rather
    /// than restarted (ADR-0025). Awaiting the task carries a failure to the grid when
    /// nobody handles <see cref="FetchFailed"/>.
    /// </summary>
    public Task OnRangeNeededAsync(RowRange range)
    {
        // The grid asks for what is on screen, so this is also how the source learns how
        // tall the Viewport is — what a restart should fetch to fill it.
        _pageRows = Math.Max(_pageRows, range.Count);
        _started = true;

        if (_disposed || Covers(range))
            return Task.CompletedTask;

        var wanted = Expand(range);
        // The same range again while it is still in flight: hand back the fetch already
        // running rather than cancelling it and asking for exactly the same thing.
        // Awaiting it is also what carries a failure to the grid.
        if (_inFlight == wanted)
            return _current;

        // Both ranges travel together: the widened one is what the server is asked for,
        // and the unwidened one is what the answer has to cover. Judging the answer by
        // the widened start accepts a page that reaches none of the rows the grid needs.
        return Start(wanted, range);
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
        // A restart IS the first fetch as far as the cold start is concerned. Left unset,
        // a Consumer that restores a saved sort before the grid binds gets this fetch and
        // then a second identical one from OnColumnsChanged, which cancels the first: a
        // wasted round trip and a loading flash on every page that pre-seeds a query.
        _started = true;
        RowSequenceVersion++;
        Window = [];
        WindowStart = 0;
        TotalCount = 0;
        // Notified whatever the loading flag was doing. The Window, the total and the
        // order's version have all moved, and none of that depends on whether a fetch
        // happened to be in flight — which, while the user is scrolling, it usually is.
        // Left to the loading flip, a filter change lands invisibly: the old rows stay on
        // screen under the new filter, with a selection ADR-0011 says must be dropped.
        var first = new RowRange(0, _pageRows);
        StartDetached(first, first, notify: true);
        Recount();
    }

    private void StartDetached(RowRange wanted, RowRange needed, bool notify = false)
        => _ = SurfaceAsync(Start(wanted, needed, notify));

    private Task Start(RowRange wanted, RowRange needed, bool notify = false)
    {
        // The previous answer can no longer be the truth, so it is cancelled and, if it
        // arrives anyway, discarded by generation below. Cancelling is a courtesy to the
        // server; the generation check is what makes it correct.
        // Cancelled but not disposed here: the superseded fetch still holds this token,
        // and a delegate that registers on it would meet an ObjectDisposedException for a
        // reason it could not see. Each run disposes its own.
        _cancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _inFlight = wanted;
        var generation = ++_generation;
        // Only when it actually flips: a second range asked for while the first is still
        // in flight moves nothing the grid paints, and an event for it would repaint
        // every row for nothing (ADR-0023's no-op principle).
        var wasLoading = IsLoading;
        IsLoading = true;
        if (notify || !wasLoading)
            StateChanged?.Invoke();
        _current = RunAsync(wanted, needed, generation, cancellation);
        return _current;
    }

    private async Task RunAsync(
        RowRange wanted, RowRange needed, int generation, CancellationTokenSource cancellation)
    {
        try
        {
            var page = await _fetch(new GridQuery(wanted, Sorts, Filter), cancellation.Token)
                .ConfigureAwait(true);
            // Late and superseded: a newer question has been asked, and this answer is to
            // the old one. Applying it would put rows on screen that nothing asked for —
            // the failure ADR-0001 names when it hands stale-answer discarding to the
            // source.
            if (generation != _generation || _disposed)
                return;
            ArgumentNullException.ThrowIfNull(page);
            Apply(needed, page);
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
            // Our own cancellation, already superseded. Nothing to report.
        }
        catch (Exception ex)
        {
            if (generation != _generation || _disposed)
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
        finally
        {
            // Each run owns its own token source and disposes it here, once every
            // awaiting continuation is through with it — never at the moment it is
            // superseded, where the fetch it belongs to is still holding the token.
            // Cleared first if it is still the current one, so the next Start does not
            // reach for a source this line has just disposed.
            if (ReferenceEquals(_cancellation, cancellation))
                _cancellation = null;
            cancellation.Dispose();
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
                $"The grid needs {asked.Count} rows at {asked.Start} and the answer is {page.Rows.Count} rows " +
                $"at {page.Start} of {page.TotalCount}, which does not reach it. A source may answer with fewer " +
                "rows than the read-ahead asked for, or with none when the result has shrunk past that " +
                "position, but the rows the grid is about to paint have to be in it (ADR-0025). A server that " +
                "caps its pages below the read-ahead window will trip this.");
        }

        // A result that shrank means the positions mean something else — row 900 is a
        // different row, or no row at all — so the selection goes, exactly as it does for
        // a reorder (ADR-0011). Growing is safe: existing positions still name the same
        // rows.
        if (TotalCount is int previous && page.TotalCount < previous)
            RowSequenceVersion++;

        var totalMoved = TotalCount != page.TotalCount;
        _inFlight = null;
        IsLoading = false;
        LastError = null;
        Window = page.Rows;
        WindowStart = page.Start;
        TotalCount = page.TotalCount;
        StateChanged?.Invoke();
        // A result that grew or shrank changed what the counts are made of.
        if (totalMoved)
            Recount();
    }

    /// <summary>
    /// Watches a fetch nobody awaited — the cold start and a restart are the source's
    /// own. A failure there still has to reach the host rather than sit in an unobserved
    /// task, so with no <see cref="FetchFailed"/> subscriber it is rethrown on the
    /// context this source was created on. <see cref="LastError"/> holds it either way.
    /// </summary>
    private async Task SurfaceAsync(Task fetch)
    {
        if (_context is null)
        {
            // Nowhere to rethrow to — a source built in a DI service or a static
            // initialiser has no dispatcher. Left faulted rather than swallowed, so
            // .NET's unobserved-exception path can still report it; LastError holds it
            // either way. ADR-0025 says such a source must subscribe to FetchFailed.
            await fetch.ConfigureAwait(false);
            return;
        }

        try
        {
            await fetch.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _context.Post(static state => ExceptionDispatchInfo.Capture((Exception)state!).Throw(), ex);
        }
    }

    /// <summary>
    /// Rows for a copy beyond the Window (ADR-0005): a one-off fetch under the Sort and
    /// Filter in force, bypassing the Window, the coalescing and the generations — this
    /// answer is handed straight to the copy and stored nowhere, so it cannot go stale
    /// in the way a painted Window can.
    /// </summary>
    public async Task<IReadOnlyList<TRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var page = await _fetch(new GridQuery(range, Sorts, Filter), cancellationToken).ConfigureAwait(true);
        ArgumentNullException.ThrowIfNull(page);
        // Trimmed to the asked range: a server free to widen its page is not free to
        // make the caller guess which of its rows were the ones asked for.
        var skip = range.Start - page.Start;
        if (skip < 0 || skip > page.Rows.Count)
            return [];
        var count = Math.Min(range.Count, page.Rows.Count - skip);
        var rows = new TRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = page.Rows[skip + i];
        return rows;
    }

    /// <summary>
    /// The value list, answered by the Consumer's own delegate — a server DISTINCT
    /// with the other columns' conditions applied (ADR-0009). Handed the Filter with
    /// this column's own spec already removed. Without a delegate the honest answer
    /// is TooMany: the panel degrades to condition mode rather than enumerating a
    /// result the source cannot see.
    /// </summary>
    public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_distinctValues is null)
            return Task.FromResult(Chrome.DistinctValues.TooMany);

        return _distinctValues(column, GridFilters.Without(Filter, column), cancellationToken);
    }

    /// <summary>
    /// Stops fetching and lets go of what is in flight. The grid does not own its Source
    /// — the Consumer built it, so the Consumer disposes it (ADR-0025). Left undisposed,
    /// a fetch outstanding at teardown goes on running and goes on raising
    /// <see cref="StateChanged"/> at whoever is still listening.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancellation?.Cancel();
    }
}
