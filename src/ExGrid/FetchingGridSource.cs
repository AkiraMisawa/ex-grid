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
///
/// <para><b>Given a Row Key, it hears that the data moved on</b> (ADR-0141): the Consumer calls
/// <see cref="NotifyChanged"/> however it learns it — SignalR, polling, a message bus — and the
/// source reads its Window again, gathered on the source's clock by the rules
/// <c>GridSource.From</c> gathers by. It pairs every answer's rows with the ones it painted by
/// key and marks the cells whose painted text changed (<see cref="CellChangedAt"/>); it refuses an
/// answer that repeats a key, by name, and so vouches that no Window holds a row twice. Only the
/// server knows whether the order of the whole result moved: an answer may carry the server's
/// order token (<see cref="GridPage{TRow}.OrderToken"/>), and the Row Sequence Version moves when
/// it differs from the previous answer's, and with every change when the server sends none.</para>
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
    private readonly Func<Finding.GridFindRequest, GridFilter?, IReadOnlyList<SortSpec>, CancellationToken, Task<Finding.GridFindResult>>? _find;

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

    // ---- Live data, under a Row Key (ADR-0141). Null or idle without one. ---------------------

    private readonly Func<TRow, object>? _key;
    private readonly ChangeGatherer? _gatherer;
    private readonly CellChangeTimes<TRow>? _changeTimes;
    // The columns the grid paints by: what the painted text of a row is read from.
    private IReadOnlyList<ColumnInfo<TRow>> _columns = [];
    // The data moved on, and no question asked since carries it.
    private bool _changeWaiting;
    // Whether the question in flight was asked after the data moved on, so its answer carries it.
    private bool _inFlightCarries;
    // The order token of the last answer, and whether an answer has landed since the last restart:
    // what the next answer's token is compared with.
    private string? _orderToken;
    private bool _answered;
    // The rows the grid last said it needs: what a Window read again must still reach.
    private RowRange? _lastNeeded;

    internal FetchingGridSource(
        Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
        int readAheadRows,
        Func<string, GridFilter?, CancellationToken, Task<Chrome.DistinctValues>>? distinctValues = null,
        Rows.RowMarkAdapter<TRow>? marks = null,
        Func<Finding.GridFindRequest, GridFilter?, IReadOnlyList<SortSpec>, CancellationToken, Task<Finding.GridFindResult>>? find = null,
        Func<TRow, object>? rowKey = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        ArgumentOutOfRangeException.ThrowIfNegative(readAheadRows);
        // The Row Mark adapter's key is a Row Key (ADR-0140): one value names a row for its marks,
        // for a source and for the grid. Two different ones would let the marks follow one key and
        // the grid pair rows by another, so both given and not the same is refused.
        if (rowKey is not null && marks is not null && !ReferenceEquals(rowKey, marks.Key))
        {
            throw new ArgumentException(
                "A Row Key and a Row Mark adapter with a key of its own were both given. The adapter's key is the " +
                "Row Key (ADR-0140): give it once, as the adapter's, or hand the same delegate to both.", nameof(rowKey));
        }
        _fetch = fetch;
        _readAheadRows = readAheadRows;
        _distinctValues = distinctValues;
        _find = find;
        Marks = marks is null ? null : new Rows.FetchingRowMarks<TRow>(this, marks);
        _key = rowKey ?? marks?.Key;
        if (_key is not null)
        {
            var time = clock ?? TimeProvider.System;
            _gatherer = new ChangeGatherer(time, OnGatherDue);
            _changeTimes = new CellChangeTimes<TRow>(_key, time);
        }
    }

    /// <summary>The Row Key this source names its rows by (ADR-0140/0141): the one it was given, or
    /// its Row Mark adapter's; null for none. The grid pairs a row's next version with the one it
    /// painted by it.</summary>
    public Func<TRow, object>? RowKey => _key;

    /// <summary>True under a Row Key: every answer is checked for a repeated or null key and refused
    /// by name, so no Window this source hands over holds a row twice (ADR-0141).</summary>
    public bool VouchesDistinctRows => _key is not null;

    /// <summary>
    /// The Change Highlight of this source (ADR-0141, on ADR-0067's rules), to hand to the grid's
    /// <c>CellChangedAt</c>; null without a Row Key. One delegate for the source's life.
    /// <list type="bullet">
    /// <item>Every answer's rows are paired by key with the rows the source painted before it, and
    /// a cell is marked when its painted text differs — the column's <c>Format</c>, as the grid
    /// paints a value cell; a change the format hides is not marked.</item>
    /// <item>On an answer that carries a change of data, a row not painted before is marked whole
    /// where it cannot have come into view by moving: between two rows painted before, or past an
    /// end of the result that the Window painted before also reached. A row at an edge of the
    /// Window may have slid in from beyond it as rows were added or removed elsewhere, and the
    /// source cannot tell that from a row that appeared, so it does not mark it: a mark never
    /// appears on a value that did not change (ADR-0067).</item>
    /// <item>A scroll, a sort or a filter marks nothing new: a sort or a filter drops the Window, and
    /// nothing is left to pair with.</item>
    /// </list>
    /// A grid's <c>PaintedText</c> declaration is not seen here: a source compares what the columns
    /// say. A change time is kept for <see cref="ChangeTimesKeptFor"/>, by key, so a mark comes back
    /// with its row when the grid scrolls away and back.
    /// </summary>
    public Cells.CellChangeOf<TRow>? CellChangedAt => _changeTimes?.Delegate;

    /// <summary>How long a cell's change time is kept for <see cref="CellChangedAt"/>: one minute
    /// unless set; set it to at least the grid's <c>ChangeHighlightDuration</c>. A fetching source
    /// never learns that a row it painted was removed — it only stops painting it — so this bound is
    /// what keeps its memory to the rows that changed lately. Refused without a Row Key.</summary>
    public TimeSpan ChangeTimesKeptFor
    {
        get => _changeTimes?.KeptFor ?? CellChangeTimes<TRow>.DefaultKeptFor;
        set => (_changeTimes ?? throw NoKey(nameof(ChangeTimesKeptFor))).KeptFor = value;
    }

    /// <summary>The shortest time between two reads of the Window for data that moved on
    /// (ADR-0141/0067): 250 ms unless set. Zero reads it for every notice, one question at a time.
    /// Negative is refused, and so is setting it without a Row Key.</summary>
    public TimeSpan GatherInterval
    {
        get => _gatherer?.Interval ?? ChangeGatherer.DefaultInterval;
        set
        {
            var gatherer = _gatherer ?? throw NoKey(nameof(GatherInterval));
            gatherer.Interval = value;
            OnContext(() =>
            {
                if (_changeWaiting && !_disposed && gatherer.ShouldAskNow(busy: _inFlight is not null))
                    AskForChange();
            });
        }
    }

    /// <summary>
    /// The Consumer says that the data behind this source moved on (ADR-0141, LV-8), however it
    /// learned it. The source reads its Window again: at once when the data has been quiet for
    /// <see cref="GatherInterval"/>, else once at the interval's end for every notice that arrived
    /// within it. A question already out is never cancelled for it; the change is read when that
    /// one lands. A question for newer data does not raise <see cref="IsLoading"/>: four reads a
    /// second would flicker the loading indication over data that is still the newest the grid has
    /// (as ExPivot's, ADR-0067). May be called on any thread.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source was given no Row Key, so it could not
    /// pair what it reads again with what it painted.</exception>
    public void NotifyChanged()
    {
        var gatherer = _gatherer ?? throw NoKey(nameof(NotifyChanged));
        if (_disposed)
            return;
        OnContext(() =>
        {
            if (_disposed)
                return;
            _changeWaiting = true;
            if (gatherer.ShouldAskNow(busy: _inFlight is not null))
                AskForChange();
        });
    }

    private static InvalidOperationException NoKey(string member) => new(
        $"{member} needs a Row Key, and this source was given none: GridSource.Fetch(..., rowKey: ...) takes one, or a " +
        "Row Mark adapter's key serves. Without one, rows read again cannot be paired with the rows painted (ADR-0141).");

    /// <summary>Runs on the context this source was built on — where its state is kept, as every
    /// answer's continuation already is — or here when there is none or this is it.</summary>
    private void OnContext(Action action)
    {
        if (_context is null || ReferenceEquals(SynchronizationContext.Current, _context))
            action();
        else
            _context.Post(static state => ((Action)state!)(), action);
    }

    /// <summary>The gathering timer fired, on the clock's thread.</summary>
    private void OnGatherDue() => OnContext(() =>
    {
        _gatherer!.Fired();
        if (_changeWaiting && !_disposed && _gatherer.ShouldAskNow(busy: _inFlight is not null))
            AskForChange();
    });

    /// <summary>Reads the Window again for data that moved on: the range the Window was asked as —
    /// wider than what came back when the result ended inside it, so a row added at the end comes
    /// into view — or the first page when there is no Window. Before the cold start there is nothing
    /// to read again: the first fetch reads the data as it is then.</summary>
    private void AskForChange()
    {
        if (!_started || _disposed)
            return;
        var wanted = _windowAsked ?? new RowRange(0, _pageRows);
        var needed = _lastNeeded is { } last && last.Start >= wanted.Start && last.Start < wanted.Start + wanted.Count
            ? last
            : new RowRange(wanted.Start, 1);
        StartDetached(wanted, needed, quiet: true);
    }

    // The range the question that brought the Window asked for; null while there is no Window.
    private RowRange? _windowAsked;

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
        // Kept for the Change Highlight, which compares the text the columns paint; a new
        // declaration marks nothing by itself (ADR-0067).
        _columns = columns.ToArray();
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
        _marksFailure = error;
        StateChanged?.Invoke();
        handler(error);
        return true;
    }

    // The failure the marks last reported, so the marks' next success can clear it — and
    // only it: a failed fetch stays until an answer lands, as it always has.
    private Exception? _marksFailure;

    /// <summary>The failure the marks last reported, or null.</summary>
    internal Exception? MarksFailure => _marksFailure;

    /// <summary>A marking went through: the failure the marks had reported when it
    /// began no longer describes the source, and a Consumer showing
    /// <see cref="LastError"/> would go on saying it is failing while everything works.
    /// Only that one — a failure reported while this marking was still counting is newer
    /// than its success, and stands.</summary>
    internal void ClearMarksFailure(Exception? seenAtStart)
    {
        if (seenAtStart is null || !ReferenceEquals(_marksFailure, seenAtStart))
            return;
        _marksFailure = null;
        if (ReferenceEquals(LastError, seenAtStart))
        {
            LastError = null;
            StateChanged?.Invoke();
        }
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
        _lastNeeded = range;

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
        // A new query's first answer is compared with no order token: the restart has moved the
        // version already.
        _orderToken = null;
        _answered = false;
        _windowAsked = null;
        // Notified whatever the loading flag was doing. The Window, the total and the
        // order's version have all moved, and none of that depends on whether a fetch
        // happened to be in flight — which, while the user is scrolling, it usually is.
        // Left to the loading flip, a filter change lands invisibly: the old rows stay on
        // screen under the new filter, with a selection ADR-0011 says must be dropped.
        var first = new RowRange(0, _pageRows);
        StartDetached(first, first, notify: true);
        Recount();
    }

    private void StartDetached(RowRange wanted, RowRange needed, bool notify = false, bool quiet = false)
        => _ = SurfaceAsync(Start(wanted, needed, notify, quiet));

    /// <param name="wanted">The range the server is asked for.</param>
    /// <param name="needed">The range its answer has to reach.</param>
    /// <param name="notify">Raise <see cref="StateChanged"/> whatever the loading flag does.</param>
    /// <param name="quiet">A read of the Window for data that moved on: it does not raise
    /// <see cref="IsLoading"/> (see <see cref="NotifyChanged"/>).</param>
    private Task Start(RowRange wanted, RowRange needed, bool notify = false, bool quiet = false)
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
        // Any question asked after the data moved on answers with the newer data, so it carries
        // the change — a scroll or a sort as much as a read of the Window for it (ADR-0067: a
        // user's gesture supersedes a question for newer data, and its own question brings the
        // change). One that supersedes a question that carried it carries it on.
        var carries = _changeWaiting || (_inFlightCarries && _inFlight is not null);
        _inFlightCarries = carries;
        _inFlight = wanted;
        var generation = ++_generation;
        if (_changeWaiting)
        {
            _changeWaiting = false;
            _gatherer?.Disarm();
        }
        // Only when it actually flips: a second range asked for while the first is still
        // in flight moves nothing the grid paints, and an event for it would repaint
        // every row for nothing (ADR-0023's no-op principle).
        var wasLoading = IsLoading;
        if (!quiet)
            IsLoading = true;
        if (notify || (!quiet && !wasLoading))
            StateChanged?.Invoke();
        _current = RunAsync(wanted, needed, generation, cancellation, carries);
        return _current;
    }

    private async Task RunAsync(
        RowRange wanted, RowRange needed, int generation, CancellationTokenSource cancellation, bool carries)
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
            Apply(needed, page, carries);
            _windowAsked = wanted;
            Landed(carries);
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
            // A change of data that could not be shown reached the screen as far as gathering is
            // concerned (ADR-0067): the next read waits the interval from here. Nothing retries
            // it on its own (ADR-0025); the next notice or scroll asks again.
            Landed(carries);
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

    /// <param name="asked">The rows the grid needs, which the answer has to reach.</param>
    /// <param name="page">The answer.</param>
    /// <param name="carries">Whether the question was asked after the data moved on.</param>
    private void Apply(RowRange asked, GridPage<TRow> page, bool carries)
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
        // Under a Row Key the answer is checked before anything of it is taken: a key that repeats,
        // or a null one, is refused by name (ADR-0141), which is what lets the source vouch.
        var keys = _key is null ? null : KeysOf(page);

        // A result that shrank means the positions mean something else — row 900 is a
        // different row, or no row at all — so the selection goes, exactly as it does for
        // a reorder (ADR-0011). Growing is safe: existing positions still name the same
        // rows.
        var moved = TotalCount is int previous && page.TotalCount < previous;
        // Only the server knows whether the order of the whole result moved (ADR-0141): its order
        // token says so when it sends one, and a server that sends none is taken to have moved it
        // with every change. Comparing the Window alone would miss a row cancelled after it.
        if (_answered)
        {
            if ((page.OrderToken is not null || _orderToken is not null)
                && !string.Equals(page.OrderToken, _orderToken, StringComparison.Ordinal))
            {
                moved = true;
            }
            if (carries && page.OrderToken is null)
                moved = true;
        }
        if (moved)
            RowSequenceVersion++;
        if (keys is not null)
            RecordChanges(page, keys, carries);
        _orderToken = page.OrderToken;
        _answered = true;

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

    /// <summary>A question landed — answered, refused or failed. One that carried a change of data
    /// brought it to the screen, or found it unshowable, and the next read waits
    /// <see cref="GatherInterval"/> from here; a change that arrived meanwhile is asked for then.</summary>
    private void Landed(bool carried)
    {
        if (_gatherer is not { } gatherer || _disposed)
            return;
        if (carried)
            gatherer.Shown();
        if (_changeWaiting && _inFlight is null && gatherer.ShouldAskNow())
            AskForChange();
    }

    /// <summary>Each row's Row Key, in order, refused by name when one repeats or is null.</summary>
    private object[] KeysOf(GridPage<TRow> page)
    {
        var keys = new object[page.Rows.Count];
        var seen = new Dictionary<object, int>(keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            var key = _key!(page.Rows[i]) ?? throw new InvalidOperationException(
                $"Row {page.Start + i} of the answer has a null Row Key. A Row Key names a row, and no row is named by " +
                "nothing (ADR-0140/0141).");
            if (!seen.TryAdd(key, i))
            {
                throw new InvalidOperationException(
                    $"Rows {page.Start + seen[key]} and {page.Start + i} of the answer both have the Row Key '{key}'. Two " +
                    "rows under one key would be painted as one, so the answer is refused (ADR-0140/0141).");
            }
            keys[i] = key;
        }
        return keys;
    }

    /// <summary>
    /// The Change Highlight of one answer (ADR-0141/0067): its rows paired by key with the Window
    /// it replaces, and the cells whose painted text differs marked; on an answer that carries a
    /// change of data, a row not painted before marked whole where it cannot have slid into view
    /// (see <see cref="CellChangedAt"/>).
    /// </summary>
    private void RecordChanges(GridPage<TRow> page, object[] keys, bool carries)
    {
        if (_changeTimes is not { Wanted: true } times || Window.Count == 0)
            return;
        var painted = new Dictionary<object, TRow>(Window.Count);
        foreach (var row in Window)
        {
            if (row is not null && _key!(row) is { } key)
                painted.TryAdd(key, row);
        }
        var pairs = new List<(object Key, TRow Old, TRow New)>();
        var appeared = new List<object>();
        // Whether the Window painted before reached an end of the result: nothing could slide in
        // past that end, so a row there was not painted because it was not there.
        var reachedTop = WindowStart == 0 && page.Start == 0;
        var reachedBottom = WindowStart + Window.Count == TotalCount && page.Start + page.Rows.Count == page.TotalCount;
        var run = -1;
        for (var i = 0; i <= keys.Length; i++)
        {
            if (i < keys.Length && !painted.ContainsKey(keys[i]))
            {
                if (run < 0)
                    run = i;
                continue;
            }
            if (i < keys.Length)
            {
                var old = painted[keys[i]];
                if (!ReferenceEquals(old, page.Rows[i]))
                    pairs.Add((keys[i], old, page.Rows[i]));
            }
            if (run >= 0)
            {
                // A run of rows not painted before, from run to i - 1, and whether rows painted
                // before stand on both sides of it, or an end of the result both Windows reached.
                var above = run > 0 || reachedTop;
                var below = i < keys.Length || reachedBottom;
                if (carries && above && below)
                {
                    for (var j = run; j < i; j++)
                        appeared.Add(keys[j]);
                }
                run = -1;
            }
        }
        times.Record(_columns, pairs, appeared, []);
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
        => (await FetchRangeAsync(range, cancellationToken).ConfigureAwait(true)).Rows;

    /// <summary>
    /// The rows of one range, trimmed to it, with the total the answer reported — which is
    /// what tells a result that shrank under the range (its positions now name other rows)
    /// from a server that answered short (ADR-0025).
    /// </summary>
    internal async Task<(IReadOnlyList<TRow> Rows, int TotalCount)> FetchRangeAsync(
        RowRange range, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var page = await _fetch(new GridQuery(range, Sorts, Filter), cancellationToken).ConfigureAwait(true);
        ArgumentNullException.ThrowIfNull(page);
        // Trimmed to the asked range: a server free to widen its page is not free to
        // make the caller guess which of its rows were the ones asked for.
        var skip = range.Start - page.Start;
        if (skip < 0 || skip > page.Rows.Count)
            return ([], page.TotalCount);
        var count = Math.Min(range.Count, page.Rows.Count - skip);
        var rows = new TRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = page.Rows[skip + i];
        return (rows, page.TotalCount);
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

    /// <summary>Whether a <c>find</c> delegate was given (ADR-0055).</summary>
    public bool CanFind => _find is not null;

    /// <summary>
    /// A Find step, answered by the <c>find</c> delegate against the Filter and Sorts in force
    /// (ADR-0055). The position it answers is in that order; the grid discards it if the order
    /// has moved since the request was read.
    /// </summary>
    public Task<Finding.GridFindResult> FindAsync(Finding.GridFindRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_find is null)
            throw new NotSupportedException("This source was given no find delegate: CanFind is false (ADR-0055).");
        return _find(request, Filter, Sorts, cancellationToken);
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
        // A read waiting for the interval's end is not asked for.
        _gatherer?.Disarm();
    }
}
