using ExGrid.Cells;

namespace ExGrid;

/// <summary>
/// <c>GridSource.From</c>: everything is in hand, so the Window is the whole
/// filtered-and-sorted result and nothing is ever loading (ADR-0001).
///
/// Columns reach the source at bind time through <see cref="OnColumnsChanged"/> — the
/// markup column declaration stays the single source of truth (ADR-0023). Until they
/// arrive the Window is the input order unchanged, and a sort or filter change is
/// refused: it cannot happen through the grid, so it is a Consumer bug.
///
/// A refused change leaves the source exactly as it was: every change is computed
/// first and committed only on success.
///
/// <para><b>Given a Row Key, it takes live data</b> (ADR-0141): a Change Batch through
/// <see cref="Apply(GridChangeBatch{TRow})"/>, or a whole new list paired by key through
/// <see cref="ReplaceAll"/>. The changes are gathered on the source's clock and reach the grid at
/// most once every <see cref="GatherInterval"/>, from the newest version, never half a batch; the
/// result is brought up to date incrementally, equal to a whole requery; the Row Sequence Version
/// moves exactly when the result's sequence moved; the source names its rows by the key
/// (<see cref="RowKey"/>), vouches that no Window holds a row twice, and answers the Change
/// Highlight (<see cref="CellChangedAt"/>). Without a key it is the source it always was.</para>
///
/// <para><b>Threads.</b> A change may be applied on any thread, and the gathering timer fires on
/// the clock's. Every change is checked and taken in under one lock, and the Window and version the
/// grid reads are replaced together on the context the source was built on — on Blazor, the
/// renderer's — which is where the grid reads them; where there is none, as on WebAssembly or in a
/// test, they are replaced on the thread that applies them. The grid's own calls (a sort, a filter,
/// <see cref="ReplaceRow"/>) arrive on that context.</para>
/// </summary>
public sealed class InMemoryGridSource<TRow> : IGridSource<TRow>, IBindsToOneCircuit
{
    CircuitBinding IBindsToOneCircuit.Binding { get; } = new();

    private readonly TRow[] _rows;
    private IReadOnlyList<ColumnInfo<TRow>>? _columns;

    // ---- Live data, under a Row Key (ADR-0141). All null or empty without one. ----------------

    private readonly Func<TRow, object>? _key;
    private readonly KeyedRows<TRow>? _keyed;
    private readonly ChangeGatherer? _gatherer;
    private readonly CellChangeTimes<TRow>? _changeTimes;
    // Where the Window and the version are replaced: the context the source was built on, where
    // the grid reads them (see the summary).
    private readonly SynchronizationContext? _context;
    private readonly object _gate = new();
    // The rows changed since the Window was last computed, by key: the version it was computed
    // from, recorded at the first change. What the next publication pairs with what is held then.
    private readonly Dictionary<object, Pending> _pending = new();
    // The Window as an array, and each row's place in the base order beside it: what the
    // incremental requery merges into.
    private TRow[] _window = [];
    private long[] _windowOrdinals = [];
    // A publication decided on and not yet run: changes arriving meanwhile go with it.
    private bool _publishing;

    internal InMemoryGridSource(IReadOnlyList<TRow> rows)
    {
        // Snapshot: the base stays immutable even if the Consumer mutates the list it
        // passed in, so the Window and the version diff baseline cannot drift silently.
        _rows = rows.ToArray();
        Window = _rows;
        Marks = new Rows.InMemoryRowMarks<TRow>(this, _rows);
    }

    internal InMemoryGridSource(IReadOnlyList<TRow> rows, Func<TRow, object> rowKey, TimeProvider clock)
    {
        _key = rowKey;
        // Keys are checked before anything is kept: a repeated or null key is refused by name.
        _keyed = new KeyedRows<TRow>(rows, rowKey);
        _rows = [];
        (_window, _windowOrdinals) = _keyed.Snapshot();
        Window = _window;
        _context = SynchronizationContext.Current;
        _gatherer = new ChangeGatherer(clock, OnGatherDue);
        _changeTimes = new CellChangeTimes<TRow>(rowKey, clock);
        Marks = new Rows.InMemoryRowMarks<TRow>(this);
    }

    /// <summary>The Row Marks of this source (ADR-0043): kept per row of the base, by
    /// identity, and carried across <see cref="ReplaceRow"/>. What a Mark Column bound to
    /// this source reads and reports to. Under a Row Key they are kept by key, and follow a
    /// row through its versions (ADR-0140).</summary>
    public Rows.InMemoryRowMarks<TRow> Marks { get; }

    Rows.IRowMarks<TRow>? IGridSource<TRow>.Marks => Marks;

    /// <summary>The Row Key this source was given (ADR-0140/0141), or null for none. The grid
    /// pairs a row's next version with the one it painted by it.</summary>
    public Func<TRow, object>? RowKey => _key;

    /// <summary>True under a Row Key: the source refuses a repeated or null key, at construction
    /// and in every change, so no Window it hands over holds a row twice, and the grid need not
    /// check (ADR-0141).</summary>
    public bool VouchesDistinctRows => _key is not null;

    /// <summary>
    /// The Change Highlight of this source (ADR-0141, on ADR-0067's rules), to hand to the grid's
    /// <c>CellChangedAt</c>; null without a Row Key, which pairs nothing. One delegate for the
    /// source's life, so only a row with a new instance asks again (LV-9).
    /// <list type="bullet">
    /// <item>A cell is marked when a changed row's painted text differs from its previous
    /// version's; a change the format hides is not marked.</item>
    /// <item>Every cell of a row that appears in the result is marked.</item>
    /// <item>A sort, a filter or a column change marks nothing; neither does the Consumer's own
    /// edit through <see cref="ReplaceRow"/>.</item>
    /// </list>
    /// The painted text is <see cref="ColumnInfo{TRow}.TextOf"/>: the column's <c>Format</c>, as the
    /// grid paints a value cell. <b>A grid's <c>PaintedText</c> declaration is not seen here</b>
    /// (ADR-0050): a source compares what the columns say, and a cell whose painted text a
    /// declaration of the grid's alone changes is marked by the values beneath it. Nothing is
    /// compared until this is first read. A change time is kept for
    /// <see cref="ChangeTimesKeptFor"/>.
    /// </summary>
    public CellChangeOf<TRow>? CellChangedAt => _changeTimes?.Delegate;

    /// <summary>
    /// How long the source keeps a cell's change time for <see cref="CellChangedAt"/>: one minute
    /// unless set. A time older than this answers nothing, so set it to at least the grid's
    /// <c>ChangeHighlightDuration</c>. Bounding it is what keeps the memory of a source whose rows
    /// all change in the end to the rows that changed lately. Set without a Row Key, it is refused:
    /// nothing is paired, so nothing is kept.
    /// </summary>
    public TimeSpan ChangeTimesKeptFor
    {
        get => _changeTimes?.KeptFor ?? CellChangeTimes<TRow>.DefaultKeptFor;
        set => (_changeTimes ?? throw NoKey(nameof(ChangeTimesKeptFor))).KeptFor = value;
    }

    /// <summary>
    /// The shortest time between two live changes reaching the grid (ADR-0141/0067): 250 ms unless
    /// set. The first change after a quiet interval reaches it at once; the changes within an
    /// interval reach it as one, at the interval's end, from the newest version. Zero passes every
    /// change as it comes. Negative is refused, and so is setting it without a Row Key, where there
    /// is nothing to gather.
    /// </summary>
    public TimeSpan GatherInterval
    {
        get => _gatherer?.Interval ?? ChangeGatherer.DefaultInterval;
        set
        {
            var gatherer = _gatherer ?? throw NoKey(nameof(GatherInterval));
            bool ask;
            lock (_gate)
            {
                gatherer.Interval = value;
                ask = Decide();
            }
            if (ask)
                Dispatch();
        }
    }

    /// <summary>The whole result: the rows under the Filter and Sorts in force, or the
    /// input order until the columns arrive (ADR-0023).</summary>
    public IReadOnlyList<TRow> Window { get; private set; }

    /// <summary>Always 0: everything is in hand, so the Window is the whole result and
    /// starts at its beginning (ADR-0001).</summary>
    public int WindowStart => 0;

    /// <summary>Post-filter count — what feeds the pager (ADR-0015). Stated rather than
    /// left null even though the Window is the whole result: a Consumer reading it for a
    /// count display should not have to know that convention.</summary>
    public int? TotalCount => Window.Count;

    /// <summary>The Sort in force, outermost first; empty for the input order. A copy of
    /// the list last applied.</summary>
    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    /// <summary>The Filter in force, or null for none — a structural copy of the one last
    /// applied, so the Consumer's own collections behind it can change without reaching
    /// this (ADR-0023).</summary>
    public GridFilter? Filter { get; private set; }

    /// <summary>Always false: everything is in hand, so no answer is ever in flight.</summary>
    public bool IsLoading => false;

    /// <summary>
    /// Identifies the order of the rows, not their values. Bumped only when the visible
    /// sequence actually differs after a change — a no-op change must not clear the
    /// selection (ADR-0011). Under a Row Key the sequence is of keys: a row replaced by a new
    /// instance under its key stands where it stood (ADR-0141, LV-7).
    /// </summary>
    public int RowSequenceVersion { get; private set; }

    /// <summary>Raised after a change the grid paints is applied — a new Sort or Filter,
    /// a moved sequence, a replaced row, live changes reaching the grid; the binding layer
    /// repushes from here. A no-op raises nothing.</summary>
    public event Action? StateChanged;

    /// <summary>Takes the grid's columns and requeries under the Filter and Sorts in
    /// force. The same columns again are a no-op, and new ones raise
    /// <see cref="StateChanged"/> only when the visible sequence moved — the grid pushed
    /// them, so it already knows.</summary>
    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        Commit(columns, Filter, Sorts);
    }

    /// <summary>Applies a new Sort by requerying, bumping
    /// <see cref="RowSequenceVersion"/> only when the visible sequence actually moved. An
    /// equal list is a no-op; before the columns have arrived the change is refused
    /// (ADR-0023).</summary>
    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);
        RequireColumns();
        Commit(_columns!, Filter, sorts);
    }

    /// <summary>Applies a new Filter by requerying, bumping
    /// <see cref="RowSequenceVersion"/> only when the visible sequence actually moved. An
    /// equal Filter is a no-op; before the columns have arrived the change is refused
    /// (ADR-0023).</summary>
    public void OnFilterChanged(GridFilter? filter)
    {
        RequireColumns();
        Commit(_columns!, filter, Sorts);
    }

    /// <summary>
    /// The Consumer's half of an Edit Intent (ADR-0007), provided by the library so
    /// nobody hand-writes it wrong: the edited row is replaced by a <b>new
    /// instance</b> — identity, not mutation, is the change signal (ADR-0003) — and
    /// the result requeries under the Filter and Sorts in force. The Row Sequence
    /// Version moves only when the visible sequence actually changed (an edit to the
    /// sorted column can move the row; ADR-0011 then drops the selection, correctly),
    /// so an ordinary value edit keeps the selection and the continuous-entry flow.
    ///
    /// <para>Under a Row Key it is a user's own edit, and it is not gathered (ADR-0141): it
    /// reaches the grid at once, bringing the live changes gathered so far with it, and it marks
    /// nothing of its own. <paramref name="row"/> must be the version the source holds under its
    /// key; one a live change has replaced since is refused by name rather than written over, since
    /// a replacement built from it would quietly undo that change. The replacement keeps the key.</para>
    /// </summary>
    public void ReplaceRow(TRow row, TRow replacement)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(replacement);
        if (ReferenceEquals(row, replacement))
        {
            throw new ArgumentException(
                "The replacement is the same instance as the row. An in-place rewrite does not reach the " +
                "screen — hand over a new instance; identity is the change signal (ADR-0003/0007).",
                nameof(replacement));
        }
        if (_keyed is not null)
        {
            ReplaceKeyedRow(row, replacement);
            return;
        }
        // Located by identity, never by value: with record rows, value equality would
        // land the edit on the first value-equal duplicate — the wrong row — and let a
        // stale equal instance pass the refusal below (ADR-0003/0007).
        var index = -1;
        for (var i = 0; i < _rows.Length; i++)
        {
            if (ReferenceEquals(_rows[i], row))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            throw new ArgumentException(
                "The row is not in this source. An Edit Intent carries the instance the grid painted; a " +
                "different or stale instance cannot be resolved onto the base (ADR-0007).", nameof(row));
        }
        RequireColumns();

        _rows[index] = replacement;
        Marks.Replaced(index, row, replacement);
        var next = GridQueryEngine.Apply(_rows, _columns!, Filter, Sorts);
        var sequenceChanged = next.Count != Window.Count;
        if (!sequenceChanged)
        {
            // The sequence compares by identity, with the one edit mapped across: at
            // the edited row's position, the old instance standing where the new one
            // now stands is the same sequence, not a reorder.
            for (var i = 0; i < next.Count; i++)
            {
                var same = ReferenceEquals(next[i], Window[i])
                    || (ReferenceEquals(next[i], replacement) && ReferenceEquals(Window[i], row));
                if (!same)
                {
                    sequenceChanged = true;
                    break;
                }
            }
        }
        if (sequenceChanged)
            RowSequenceVersion++;
        Window = next;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Applies a Change Batch whole (ADR-0141, LV-3): the rows it changes, found by their Row Key,
    /// each keep the place in the base order of the row they replace; the rows it adds go at the
    /// end; the keys it removes leave. A key that repeats — within the batch, or an added key the
    /// source already holds — a changed or removed key the source does not hold, a null key, a
    /// changed row that is the instance already held, or a row whose values the Query in force
    /// refuses, refuses the whole batch by name, and nothing of it is applied.
    ///
    /// <para>It is gathered: the first change after a quiet <see cref="GatherInterval"/> reaches the
    /// grid at once, and the changes after it within the interval reach it as one at its end, from
    /// the newest version (LV-6). The grid never sees half a batch. May be called on any
    /// thread.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The source was given no Row Key.</exception>
    /// <exception cref="ArgumentException">The batch is refused; the message names the key.</exception>
    public void Apply(GridChangeBatch<TRow> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var keyed = _keyed ?? throw NoKey(nameof(Apply));
        bool ask;
        lock (_gate)
        {
            var plan = PlanOf(keyed, batch);
            if (plan.IsEmpty)
                return;
            Fold(keyed, plan, byUser: false);
            ask = Decide();
        }
        if (ask)
            Dispatch();
    }

    /// <summary>
    /// Takes a whole new list, paired with the rows held by Row Key (ADR-0141, LV-4), as ag-grid
    /// takes new <c>rowData</c> under <c>getRowId</c>: a new key is a row added, a key missing is a
    /// row removed, a different instance under a key is a row changed, and the same instance is a
    /// row unchanged. It is the Change Batch that says the same, applied the same way: the result
    /// and the Row Sequence Version are that batch's. The list's own order is not taken: a row
    /// keeps its place in the base order, and the rows added go at its end in the list's order.
    /// Pairing compares references, never values, so it reads no value of any row. A key that
    /// repeats in the list, or a null one, refuses the list by name.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source was given no Row Key.</exception>
    public void ReplaceAll(IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var keyed = _keyed ?? throw NoKey(nameof(ReplaceAll));
        bool ask;
        lock (_gate)
        {
            var plan = PairWithHeld(keyed, rows);
            if (plan.IsEmpty)
                return;
            Fold(keyed, plan, byUser: false);
            ask = Decide();
        }
        if (ask)
            Dispatch();
    }

    /// <summary>Everything was pushed up front, so a Range Request needs no answer — a
    /// range beyond the data is legal (requests race with data updates), and ignoring
    /// it is the answer (ADR-0001). A malformed range never gets here: the
    /// <see cref="RowRange"/> constructor refuses it.</summary>
    public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

    /// <summary>
    /// Past this many distinct values the answer is TooMany (ADR-0009): the panel
    /// degrades to a search-and-condition form, which is Excel's own shape for a large
    /// domain. Provisional the way ADR-0004's thresholds are.
    /// </summary>
    public const int DistinctValueCap = 1000;

    /// <summary>
    /// The reference semantics of the value list (ADR-0009): distinct values of the
    /// column under all applied filters except its own, in order of first appearance.
    /// Blanks appear as a null entry, so the panel can offer them (ADR-0023).
    /// </summary>
    public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        RequireColumns();
        var info = _columns!.FirstOrDefault(c => c.Name == column)
            ?? throw new InvalidOperationException($"No column is named '{column}'.");

        var all = BaseRows();
        var others = GridFilters.Without(Filter, column);
        var rows = others is null ? all : GridQueryEngine.Apply(all, _columns!, others, []);

        var seen = new HashSet<object?>();
        var values = new List<object?>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = info.Value(row);
            if (seen.Add(value))
            {
                values.Add(value);
                if (values.Count > DistinctValueCap)
                    return Task.FromResult(Chrome.DistinctValues.TooMany);
            }
        }
        return Task.FromResult(Chrome.DistinctValues.Of(values));
    }

    /// <summary>Always: every row is in hand (ADR-0055).</summary>
    public bool CanFind => true;

    /// <summary>
    /// The reference Find (ADR-0055): <see cref="Finding.GridFind.Step{TRow}"/> over the
    /// current result, matching each column's displayed text as the grid handed it over in
    /// <see cref="ColumnInfo{TRow}.TextOf(TRow)"/>. A column this source was not told about, or one
    /// with no text of its own, is skipped. A request read under another version is answered
    /// anyway: the grid discards it by the version, which is the one place that knows.
    /// </summary>
    public Task<Finding.GridFindResult> FindAsync(Finding.GridFindRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var columns = _columns ?? [];
        return Task.FromResult(Finding.GridFind.Step(Window, request, name =>
        {
            foreach (var column in columns)
            {
                if (column.Name == name)
                    return column.IsQueryable ? column.TextOf : null;
            }
            return null;
        }));
    }

    /// <summary>Everything is in hand, so a copy beyond the Window cannot arise — but
    /// the answer is honest anyway: the slice of the current result, clamped to what
    /// exists (ADR-0005).</summary>
    public Task<IReadOnlyList<TRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        var window = Window;
        var start = Math.Min(range.Start, window.Count);
        var count = Math.Min(range.Count, window.Count - start);
        var rows = new TRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = window[start + i];
        return Task.FromResult<IReadOnlyList<TRow>>(rows);
    }

    private void RequireColumns()
    {
        if (_columns is null)
            throw new InvalidOperationException(
                "Columns have not been received yet; call OnColumnsChanged first. " +
                "Bound to an ExGrid, this cannot happen — the grid pushes its columns at bind time.");
    }

    private static InvalidOperationException NoKey(string member) => new(
        $"{member} needs a Row Key, and this source was given none: GridSource.From(rows, rowKey) takes live data, " +
        "and pairs a row's versions by the key (ADR-0141).");

    /// <summary>The rows the base holds, in base order.</summary>
    private IReadOnlyList<TRow> BaseRows()
    {
        if (_keyed is null)
            return _rows;
        lock (_gate)
            return _keyed.Snapshot().Rows;
    }

    private void Commit(
        IReadOnlyList<ColumnInfo<TRow>> columns,
        GridFilter? filter,
        IReadOnlyList<SortSpec> sorts)
    {
        // A byte-identical repush changes nothing — no recompute, no event. Without
        // this, a binding layer that repushes on every render and re-renders on
        // StateChanged would spin (ADR-0023's no-op principle, applied to the commit).
        // Columns compare element-wise, never by list reference: _columns is a snapshot
        // (a different instance by construction), while ColumnInfo's record equality —
        // name, type, and the accessor delegate's identity — is exactly what "the same
        // columns repushed" means for a grid that caches its column objects (ADR-0003).
        var sortsChanged = !sorts.SequenceEqual(Sorts);
        var filterChanged = !GridFilters.Equal(filter, Filter);
        var columnsChanged = _columns is null || !columns.SequenceEqual(_columns);
        if (!sortsChanged && !filterChanged && !columnsChanged)
            return;

        if (_keyed is not null)
        {
            CommitKeyed(_keyed, columns, filter, sorts, sortsChanged || filterChanged);
            return;
        }

        // Compute first — a refusal from the engine must leave the source usable.
        var next = GridQueryEngine.Apply(_rows, columns, filter, sorts);

        // Snapshot everything, for the same reason the constructor snapshots the rows: a
        // Consumer reusing and mutating its collections must not desync the stored query
        // from the Window it was applied to, nor be replayed by the next unrelated
        // change. The filter's read-only interfaces are typically backed by the
        // Consumer's live Dictionary/List, so it is copied structurally too.
        _columns = columns.ToArray();
        Filter = GridFilters.Snapshot(filter);
        Sorts = sorts.ToArray();
        var sequenceChanged = !next.SequenceEqual(Window);
        if (sequenceChanged)
            RowSequenceVersion++;
        Window = next;
        // Notify only when something observable moved. Columns alone carry no event:
        // the grid pushed them, so it already knows.
        if (sequenceChanged || sortsChanged || filterChanged)
            StateChanged?.Invoke();
    }

    // ---- Live data (ADR-0141) ------------------------------------------------------------------

    /// <summary>A row changed since the Window was last computed: the version it was computed
    /// from, if the base held one then. <c>MarkAgainst</c> is the version just before the
    /// Consumer's own edit (<see cref="ReplaceRow"/>): the Change Highlight compares up to it, so
    /// the edit marks nothing of its own.</summary>
    private sealed class Pending(bool hadOld, TRow old, long oldOrdinal)
    {
        public bool HadOld { get; } = hadOld;

        public TRow Old { get; } = old;

        public long OldOrdinal { get; } = oldOrdinal;

        public bool ByUser { get; set; }

        public TRow MarkAgainst { get; set; } = default!;
    }

    /// <summary>What a batch or a list does, checked whole before any of it is taken in.</summary>
    private sealed record ChangePlan(
        List<(object Key, TRow Row)> Added,
        List<(object Key, TRow Row)> Changed,
        List<object> Removed,
        List<TRow> Checked)
    {
        public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && Removed.Count == 0;
    }

    /// <summary>Checks a batch against what is held, all of it, before anything is taken in
    /// (LV-3): a refusal leaves the source exactly as it was.</summary>
    private ChangePlan PlanOf(KeyedRows<TRow> keyed, GridChangeBatch<TRow> batch)
    {
        var plan = new ChangePlan([], [], [], []);
        var seen = new HashSet<object>();
        void Once(object key, string where)
        {
            if (!seen.Add(key))
            {
                throw new ArgumentException(
                    $"The Change Batch names the Row Key '{key}' more than once ({where}). A batch says one thing " +
                    "of each row; nothing of it was applied (ADR-0141).", nameof(batch));
            }
        }
        for (var i = 0; i < batch.RemovedKeys.Count; i++)
        {
            var key = batch.RemovedKeys[i];
            Once(key, $"removed key {i}");
            if (!keyed.Holds(key))
            {
                throw new ArgumentException(
                    $"The Change Batch removes the Row Key '{key}', and this source holds no row under it. " +
                    "Nothing of the batch was applied (ADR-0141).", nameof(batch));
            }
            plan.Removed.Add(key);
        }
        for (var i = 0; i < batch.Changed.Count; i++)
        {
            var row = batch.Changed[i];
            var key = keyed.KeyOf(row, $"Changed row {i}");
            Once(key, $"changed row {i}");
            if (!keyed.TryGet(key, out var held, out _))
            {
                throw new ArgumentException(
                    $"The Change Batch changes the row under the Row Key '{key}', and this source holds no row under " +
                    "it; a new row is added, not changed. Nothing of the batch was applied (ADR-0141).", nameof(batch));
            }
            if (ReferenceEquals(held, row))
            {
                throw new ArgumentException(
                    $"The Change Batch changes the row under the Row Key '{key}' to the instance already held. A row " +
                    "rewritten in place does not reach the screen; a change is a new instance (ADR-0003/0141). " +
                    "Nothing of the batch was applied.", nameof(batch));
            }
            plan.Changed.Add((key, row));
            plan.Checked.Add(row);
        }
        for (var i = 0; i < batch.Added.Count; i++)
        {
            var row = batch.Added[i];
            var key = keyed.KeyOf(row, $"Added row {i}");
            Once(key, $"added row {i}");
            if (keyed.Holds(key))
            {
                throw new ArgumentException(
                    $"The Change Batch adds a row under the Row Key '{key}', which this source already holds; two rows " +
                    "under one key would be painted as one (ADR-0140). A new version is a changed row. Nothing of the " +
                    "batch was applied (ADR-0141).", nameof(batch));
            }
            plan.Added.Add((key, row));
            plan.Checked.Add(row);
        }
        CheckValues(plan.Checked);
        return plan;
    }

    /// <summary>A whole new list, paired with what is held by key (LV-4).</summary>
    private ChangePlan PairWithHeld(KeyedRows<TRow> keyed, IReadOnlyList<TRow> rows)
    {
        var plan = new ChangePlan([], [], [], []);
        var listed = new Dictionary<object, int>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? throw new ArgumentException($"Row {i} of the list is null.", nameof(rows));
            var key = keyed.KeyOf(row, $"Row {i} of the list");
            if (!listed.TryAdd(key, i))
            {
                throw new ArgumentException(
                    $"Rows {listed[key]} and {i} of the list both have the Row Key '{key}'; two rows under one key " +
                    "would be painted as one (ADR-0140). Nothing of the list was taken (ADR-0141).", nameof(rows));
            }
            if (!keyed.TryGet(key, out var held, out _))
            {
                plan.Added.Add((key, row));
                plan.Checked.Add(row);
            }
            else if (!ReferenceEquals(held, row))
            {
                plan.Changed.Add((key, row));
                plan.Checked.Add(row);
            }
        }
        // Every held key the list names is either kept or changed; when fewer are named than are
        // held, the rest are removed.
        if (listed.Count - plan.Added.Count < keyed.Count)
        {
            foreach (var row in keyed.Snapshot().Rows)
            {
                var key = keyed.KeyOf(row, "A row held");
                if (!listed.ContainsKey(key))
                    plan.Removed.Add(key);
            }
        }
        CheckValues(plan.Checked);
        return plan;
    }

    /// <summary>The new rows' values, held to the Query in force as a requery would hold them: a
    /// value whose type contradicts its column's declaration is refused now, by name, rather than
    /// when the change reaches the grid (ADR-0023).</summary>
    private void CheckValues(List<TRow> rows)
    {
        if (_columns is not null && rows.Count > 0)
            GridQueryEngine.ApplyPositions(rows, _columns, Filter, Sorts);
    }

    /// <summary>Takes a checked plan in: the base moves to the newest version, and each key it
    /// touches remembers the version the Window was computed from.</summary>
    private void Fold(KeyedRows<TRow> keyed, ChangePlan plan, bool byUser)
    {
        foreach (var key in plan.Removed)
        {
            Remember(keyed, key);
            keyed.Remove(key);
            Marks.Removed(key);
        }
        foreach (var (key, row) in plan.Changed)
        {
            var pending = Remember(keyed, key);
            if (byUser)
            {
                pending.ByUser = true;
                keyed.TryGet(key, out var before, out _);
                pending.MarkAgainst = before;
            }
            keyed.Change(key, row);
        }
        foreach (var (key, row) in plan.Added)
        {
            Remember(keyed, key);
            keyed.Add(key, row);
        }
    }

    private Pending Remember(KeyedRows<TRow> keyed, object key)
    {
        if (!_pending.TryGetValue(key, out var pending))
        {
            pending = keyed.TryGet(key, out var row, out var ordinal)
                ? new Pending(true, row, ordinal)
                : new Pending(false, default!, -1);
            _pending.Add(key, pending);
        }
        return pending;
    }

    /// <summary>Under the lock: whether the changes waiting should be published now. When so, the
    /// publication is taken on, and changes arriving before it runs go with it.</summary>
    private bool Decide()
    {
        if (_pending.Count == 0 || !_gatherer!.ShouldAskNow(busy: _publishing))
            return false;
        _publishing = true;
        return true;
    }

    /// <summary>Publishes on the source's context, or here when there is none or this is it.</summary>
    private void Dispatch()
    {
        if (_context is null || ReferenceEquals(SynchronizationContext.Current, _context))
            PublishGathered();
        else
            _context.Post(static state => ((InMemoryGridSource<TRow>)state!).PublishGathered(), this);
    }

    /// <summary>The gathering timer fired, on the clock's thread.</summary>
    private void OnGatherDue()
    {
        bool ask;
        lock (_gate)
        {
            _gatherer!.Fired();
            ask = Decide();
        }
        if (ask)
            Dispatch();
    }

    private void PublishGathered()
    {
        bool raise;
        lock (_gate)
        {
            _publishing = false;
            raise = PublishPending();
        }
        if (raise)
            StateChanged?.Invoke();
    }

    /// <summary>
    /// Under the lock: the Window brought up to the newest version from the changes gathered, by
    /// the incremental requery; the version moved when the sequence did (LV-7); the Change
    /// Highlight recorded (LV-9). True when the grid has something new to paint.
    /// </summary>
    private bool PublishPending()
    {
        if (_pending.Count == 0)
            return false;
        var keyed = _keyed!;
        var changes = new List<RowChange<TRow>>(_pending.Count);
        var pendings = new List<Pending>(_pending.Count);
        foreach (var (key, pending) in _pending)
        {
            var hasNew = keyed.TryGet(key, out var row, out var ordinal);
            // Changed and changed back, or added and removed again, within one gathering: nothing
            // to show.
            if (pending.HadOld ? hasNew && ReferenceEquals(pending.Old, row) && pending.OldOrdinal == ordinal : !hasNew)
                continue;
            changes.Add(new RowChange<TRow>(key, pending.HadOld, pending.Old, pending.OldOrdinal, hasNew, row, ordinal));
            pendings.Add(pending);
        }
        _pending.Clear();
        _gatherer!.Shown();
        if (changes.Count == 0)
            return false;

        var outcome = LiveRequery.Apply(
            _window, _windowOrdinals, changes, _columns ?? [], Filter, Sorts, keyed.Count, keyed.Snapshot);
        RecordChanges(changes, pendings, outcome);

        var windowChanged = !ReferenceEquals(outcome.Result, _window);
        if (outcome.Moved)
            RowSequenceVersion++;
        _window = outcome.Result;
        _windowOrdinals = outcome.Ordinals;
        Window = _window;
        Marks.RowsChanged();
        return windowChanged || outcome.Moved;
    }

    /// <summary>The Change Highlight of one publication (ADR-0141/0067, LV-9): a row that stood in
    /// the result and still does is compared cell by cell; one that was not in it and now is has
    /// appeared, and every cell is marked; a key removed is forgotten.</summary>
    private void RecordChanges(List<RowChange<TRow>> changes, List<Pending> pendings, LiveRequeryOutcome<TRow> outcome)
    {
        var times = _changeTimes!;
        if (!times.Wanted)
            return;
        var pairs = new List<(object Key, TRow Old, TRow New)>();
        var appeared = new List<object>();
        var removed = new List<object>();
        for (var j = 0; j < changes.Count; j++)
        {
            var change = changes[j];
            if (!change.HasNew)
            {
                removed.Add(change.Key);
                continue;
            }
            if (outcome.NewIndex[j] < 0)
                continue;
            var pending = pendings[j];
            if (outcome.OldIndex[j] >= 0)
                pairs.Add((change.Key, change.Old, pending.ByUser ? pending.MarkAgainst : change.New));
            else if (!pending.ByUser)
                appeared.Add(change.Key);
        }
        times.Record(_columns ?? [], pairs, appeared, removed);
    }

    /// <summary>The Consumer's own edit under a Row Key: not gathered, so it reaches the grid at
    /// once with whatever was gathered.</summary>
    private void ReplaceKeyedRow(TRow row, TRow replacement)
    {
        var keyed = _keyed!;
        bool raise;
        lock (_gate)
        {
            var key = keyed.KeyOf(row, "The row");
            if (!keyed.TryGet(key, out var held, out _))
            {
                throw new ArgumentException(
                    $"The row is not in this source: it holds no row under the Row Key '{key}'. An Edit Intent carries " +
                    "the instance the grid painted (ADR-0007/0141).", nameof(row));
            }
            if (!ReferenceEquals(held, row))
            {
                throw new ArgumentException(
                    $"The row under the Row Key '{key}' changed since this version of it was taken: a live change " +
                    "replaced it. A replacement built from it would quietly undo that change, so it is refused " +
                    "(ADR-0141/0142); build the edit on the row as the source holds it now.", nameof(row));
            }
            var replacementKey = keyed.KeyOf(replacement, "The replacement");
            if (!Equals(replacementKey, key))
            {
                throw new ArgumentException(
                    $"The replacement has the Row Key '{replacementKey}', and the row it replaces '{key}'. A Row Key " +
                    "stays the same when a row's data changes (ADR-0140); a row under another key is a row removed " +
                    "and a row added, a Change Batch.", nameof(replacement));
            }
            RequireColumns();
            CheckValues([replacement]);
            Fold(keyed, new ChangePlan([], [(key, replacement)], [], []), byUser: true);
            raise = PublishPending();
        }
        if (raise)
            StateChanged?.Invoke();
    }

    /// <summary>
    /// A new Query under a Row Key: the changes gathered so far are published first, under the Query
    /// they were gathered under, so their cells are marked; then the result is requeried whole under
    /// the new one, which marks nothing (ADR-0067). The version moves when the result's sequence of
    /// keys differs from the one the grid had.
    /// </summary>
    private void CommitKeyed(
        KeyedRows<TRow> keyed,
        IReadOnlyList<ColumnInfo<TRow>> columns,
        GridFilter? filter,
        IReadOnlyList<SortSpec> sorts,
        bool queryChanged)
    {
        bool raise;
        lock (_gate)
        {
            // What the grid has: the comparison is with it, whatever the gathered changes did on
            // the way, since the grid sees this whole commit as one change.
            var before = _window;
            var version = RowSequenceVersion;
            // Compute first — a refusal from the engine must leave the source usable.
            var (rows, ordinals) = keyed.Snapshot();
            var positions = GridQueryEngine.ApplyPositions(rows, columns, filter, sorts);

            PublishPending();
            var next = new TRow[positions.Length];
            var nextOrdinals = new long[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                next[i] = rows[positions[i]];
                nextOrdinals[i] = ordinals[positions[i]];
            }
            _columns = columns.ToArray();
            Filter = GridFilters.Snapshot(filter);
            Sorts = sorts.ToArray();
            var sequenceChanged = !SameKeys(before, next);
            RowSequenceVersion = sequenceChanged ? version + 1 : version;
            var windowChanged = !SameRows(before, next);
            // An unchanged result keeps its instance, so the grid has nothing new to take; its
            // ordinals are the same rows' either way.
            _window = windowChanged ? next : before;
            _windowOrdinals = nextOrdinals;
            Window = _window;
            // Notify only when something observable moved. Columns alone carry no event: the grid
            // pushed them, so it already knows.
            raise = sequenceChanged || queryChanged || windowChanged;
        }
        if (raise)
            StateChanged?.Invoke();
    }

    private bool SameKeys(TRow[] a, TRow[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]) && !Equals(_key!(a[i]), _key!(b[i])))
                return false;
        }
        return true;
    }

    private static bool SameRows(TRow[] a, TRow[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        }
        return true;
    }

    // ---- What the marks need of a keyed source --------------------------------------------------

    /// <summary>A row's Row Key, or null where it has none.</summary>
    internal object? KeyOrNull(TRow row) => row is null ? null : _key?.Invoke(row);

    /// <summary>The row held under <paramref name="key"/> now.</summary>
    internal bool TryGetHeld(object key, out TRow row)
    {
        if (_keyed is null)
        {
            row = default!;
            return false;
        }
        lock (_gate)
            return _keyed.TryGet(key, out row, out _);
    }

    /// <summary>The rows held now, in base order.</summary>
    internal IReadOnlyList<TRow> HeldRows() => BaseRows();
}
