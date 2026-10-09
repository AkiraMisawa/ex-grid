using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>One complete computation or a named refusal (ADR-0153).</summary>
public sealed class PivotComputationResult
{
    internal PivotComputationResult(PivotReport? report, PivotSourceRefusal? refusal, bool reset,
        IReadOnlyList<PivotReportRow>? labels = null,
        IReadOnlyList<PivotRowKey>? removed = null)
    {
        Report = report;
        Refusal = refusal;
        IsReset = reset;
        LabelChanges = labels ?? [];
        RemovedRows = removed ?? [];
    }

    /// <summary>The complete immutable report, or null on refusal.</summary>
    public PivotReport? Report { get; }
    /// <summary>The reason no report was published.</summary>
    public PivotSourceRefusal? Refusal { get; }
    /// <summary>Whether computation was refused.</summary>
    public bool IsRefused => Refusal is not null;
    /// <summary>Whether the report structure was initialized for a new question, layout, display setting or reset.</summary>
    public bool IsReset { get; }
    /// <summary>Added rows and rows whose labels changed; reset callers initialize from all rows.</summary>
    public IReadOnlyList<PivotReportRow> LabelChanges { get; }
    /// <summary>Keys removed from the report by a structural update.</summary>
    public IReadOnlyList<PivotRowKey> RemovedRows { get; }
    /// <summary>Whether the published row key sequence changed from this session's preceding result.</summary>
    public bool RowSequenceChanged { get; internal init; }
}

/// <summary>
/// A report's independent computation state (ADR-0153). Snapshot changes are folded into this
/// session's aggregate pass. Published reports never change. Disposing it leaves the provider alive.
/// <para>
/// <b>Cancellation.</b> A computation is observed to be cancelled only where the session's own
/// state is whole, so a cancelled one never costs the next its incremental state. Before any of it
/// changes, a cancellation stops the work and nothing is kept. Folding the pending Change Batches
/// into the held computation changes its working indexes in place, and from the first batch folded
/// the work goes on, still yielding between slices, until the cube and the report have followed
/// and the result is returned: a cancellation that lands meanwhile is not observed. A new
/// computation — a new question, a compaction, a provider's answer — and a report laid out afresh
/// are built aside and adopted together at the end, so cancelling one leaves the previous state as
/// it was. Every state the session keeps is a state it returned: a result's changes are relative to
/// the preceding result returned, whenever the cancellations landed.
/// </para>
/// </summary>
public sealed class PivotComputationSession : IDisposable
{
    private readonly PivotSource _source;
    private readonly IReadOnlyList<PivotField> _fields;
    private readonly SemaphoreSlim _compute = new(1);
    private readonly Lock _gate = new();
    private readonly List<SnapshotChange> _pending = [];
    private AggregationPass? _pass;
    private ComputationCube? _computation;
    // The report last returned, and its structure: always laid out from the newest cube the
    // session holds, so the held data and the report shown from it are one.
    private PivotReport? _report;
    private ComputationReport? _structure;
    private string? _layout;
    private PivotOptions? _options;
    private volatile bool _disposed;
    private long _sourceEpoch;
    private long _computedEpoch = -1;

    /// <summary>Creates independent report state. Optional declarations override display and order metadata.</summary>
    public PivotComputationSession(PivotSource source, IReadOnlyList<PivotField>? fields = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _fields = fields ?? source.Fields;
        source.ComputationChanged += ProviderChanged;
        if (source is SnapshotPivotSource snapshot)
            snapshot.SnapshotChanged += Changed;
    }

    /// <summary>The newest cube the session holds — the one the report it last returned was laid
    /// out from — or null before its first result.</summary>
    internal PivotCube? HeldCube => _report?.Cube;

    private void ProviderChanged(PivotSourceChanged change) => Interlocked.Increment(ref _sourceEpoch);

    private void Changed(SnapshotChange change)
    {
        lock (_gate)
            if (!_disposed)
                _pending.Add(change);
    }

    /// <summary>Computes the latest complete report under the requested layout and display settings.</summary>
    /// <param name="layout">The layout to lay the report out under.</param>
    /// <param name="options">The culture and words; <see cref="PivotOptions.Default"/> when left out.</param>
    /// <param name="maxLeaves">The cap on the answer's leaves.</param>
    /// <param name="slicing">How the work shares the thread.</param>
    /// <param name="cancellationToken">Stops the work where the session's state is whole: before
    /// the held computation changes, and anywhere in a computation or a layout built aside. Once
    /// pending batches are being folded in place, the computation finishes and is returned.</param>
    /// <param name="refreshData">Asks a provider that is not a Snapshot again, though it announced no change.</param>
    /// <param name="preferHeld">Lays the layout out from the cube held, without folding the pending
    /// batches, when that cube holds it: a layout gesture shows the data it was made on.</param>
    public async ValueTask<PivotComputationResult> ComputeAsync(PivotLayout layout, PivotOptions? options = null,
        int maxLeaves = 200_000, PivotSlicing? slicing = null, CancellationToken cancellationToken = default,
        bool refreshData = false, bool preferHeld = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _compute.WaitAsync(cancellationToken).ConfigureAwait(false);
        // Set while the held computation is changed in place: a failure then — never a
        // cancellation, which is not observed there — leaves it half made, and it is dropped.
        var inPlace = false;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var query = PivotQuery.For(layout, maxLeaves);
            var sourceEpoch = Volatile.Read(ref _sourceEpoch);
            if (_source.Refusal(query) is { } refusal)
                return new(null, refusal, false);
            var chosen = options ?? PivotOptions.Default;
            var layoutName = PivotLayoutJson.Write(layout);
            var pacing = slicing ?? PivotSlicing.Default;
            // Where a cancellation is observed: work that changes nothing the session keeps.
            var aside = Slicer.Of(pacing, cancellationToken);
            // Where it is not: the work that carries batches folded in place through to a whole state.
            Slicer? whole = null;
            PivotCube cube;
            // A new computation, built aside and adopted with the report.
            AggregationPass? newPass = null;
            ComputationCube? newComputation = null;
            var updated = false;
            var answered = false;
            if (preferHeld && _report is { } held && held.Cube.Holds(layout) && held.Cube.Query.MaxLeaves == maxLeaves)
                cube = held.Cube;
            else if (_source is SnapshotPivotSource source)
            {
                var captured = source.Capture();
                SnapshotChange[] changes;
                lock (_gate)
                    changes = _pending.Where(c => c.After.Version <= captured.Snapshot.Version).OrderBy(c => c.After.Version).ToArray();
                var pass = _pass;
                var incremental = pass is not null && _computation is not null && _report is not null && pass.Query.Equals(query);
                if (incremental && changes.Any(c => c.After.Version > pass!.Snapshot.Version))
                {
                    inPlace = true;
                    whole = Slicer.Of(pacing, CancellationToken.None);
                    var leaves = new HashSet<int>();
                    var items = pass!.Axes.Select(_ => new HashSet<int>()).ToArray();
                    foreach (var change in changes)
                    {
                        if (change.After.Version <= pass.Snapshot.Version)
                            continue;
                        if (!pass.Fold(change))
                        {
                            // A compaction moved the rows, or the leaves passed the cap: the pass
                            // may be half folded, and is dropped before anything is awaited. The
                            // report held is whole and stays; the question is asked afresh below.
                            _pass = null;
                            _computation = null;
                            inPlace = false;
                            incremental = false;
                            break;
                        }
                        leaves.UnionWith(pass.ChangedLeaves);
                        for (var i = 0; i < items.Length; i++) items[i].UnionWith(pass.Axes[i].ChangedItems);
                    }
                    if (incremental)
                    {
                        pass.ChangedLeaves.UnionWith(leaves);
                        for (var i = 0; i < items.Length; i++) pass.Axes[i].ChangedItems.UnionWith(items[i]);
                        updated = true;
                    }
                }
                if (updated)
                    cube = await _computation!.UpdateAsync(pass!, source.VersionOf(pass!.Snapshot), whole!).ConfigureAwait(false);
                else if (incremental)
                    cube = _report!.Cube;
                else
                {
                    var bindings = FieldBinding.Of(captured.Snapshot, source.Fields).Item2;
                    newPass = new AggregationPass(captured.Snapshot, bindings, query,
                        captured.Snapshot.RecordKey is not null, pacing.RowsRead);
                    await aside.PassAsync(newPass.RowCount, newPass.Step).ConfigureAwait(false);
                    await newPass.CompleteAsync(aside).ConfigureAwait(false);
                    var answer = await newPass.AnswerAsync(captured.Version, aside).ConfigureAwait(false);
                    if (answer.IsRefused)
                        return new(null, answer.Refusal, true);
                    // The cube PivotEngine.CubeAsync makes, and the nodes it made for each leaf,
                    // which the computation starts from.
                    var leaves = new PivotCube.LeafNodes();
                    cube = await PivotCube.BuildAsync(query, answer, FieldMeta.Of(_fields),
                        Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken), leafNodes: leaves).ConfigureAwait(false);
                    newComputation = await ComputationCube.CreateAsync(cube, leaves, newPass, aside).ConfigureAwait(false);
                }
            }
            else
            {
                if (!refreshData && _computedEpoch == sourceEpoch && _report is not null
                    && _report.Cube.Holds(layout) && _report.Cube.Query.MaxLeaves == maxLeaves)
                    cube = _report.Cube;
                else
                {
                    answered = true;
                    var answer = await _source.AggregateAsync(query, cancellationToken).AsTask()
                        .WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (answer.IsRefused)
                        return new(null, answer.Refusal, true);
                    cube = await PivotEngine.CubeAsync(query, answer, _fields, slicing, cancellationToken).ConfigureAwait(false);
                }
            }
            var fresh = newComputation is not null || answered;
            var relayout = fresh || _report is null || _structure is null || _layout != layoutName || !Equals(_options, chosen);
            PivotReport report;
            ComputationReport structure;
            if (relayout)
            {
                // Laid out afresh, aside: a cancellation drops it, unless the batches it follows
                // were folded in place, which it then completes. The layout is checked as
                // PivotEngine.ReportAsync checks it.
                var work = updated ? whole! : aside;
                var builder = PivotEngine.Builder(cube, layout, chosen);
                if (!updated)
                    cancellationToken.ThrowIfCancellationRequested();
                structure = new ComputationReport();
                report = await structure.InitializeAsync(builder, work).ConfigureAwait(false);
            }
            else if (updated)
            {
                structure = _structure!;
                report = await structure.UpdateAsync(_report!, cube, _computation!, whole!).ConfigureAwait(false);
            }
            else
            {
                structure = _structure!;
                report = _report!;
            }

            // Adopted together; nothing below awaits.
            if (newComputation is not null)
            {
                _pass = newPass;
                _computation = newComputation;
            }
            if (_source is SnapshotPivotSource snapshotSource && _pass is { } kept)
            {
                snapshotSource.RememberComputation(kept.Snapshot);
                lock (_gate)
                    _pending.RemoveAll(c => c.After.Version <= kept.Snapshot.Version);
            }
            _report = report;
            _structure = structure;
            _layout = layoutName;
            _options = chosen;
            if (answered) _computedEpoch = sourceEpoch;
            inPlace = false;
            return new(report, null, relayout, labels: updated && !relayout ? structure.LabelChanges.ToArray() : [],
                removed: updated && !relayout ? structure.RemovedRows.ToArray() : [])
            { RowSequenceChanged = relayout || (updated && structure.RowSequenceChanged) };
        }
        catch when (inPlace)
        {
            // A failure while the held computation was changed in place — memory running out, an
            // Order Key that throws — leaves it half made: nothing of it is kept, and the next
            // computation starts afresh. The reports already returned are immutable and unharmed.
            _pass = null;
            _computation = null;
            _report = null;
            _structure = null;
            _layout = null;
            _options = null;
            throw;
        }
        finally
        {
            if (_disposed) Clear();
            _compute.Release();
        }
    }

    /// <summary>Stops observing changes and releases the session's computation; the source stays alive.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _pending.Clear();
        }
        if (_source is SnapshotPivotSource snapshot)
            snapshot.SnapshotChanged -= Changed;
        _source.ComputationChanged -= ProviderChanged;
        // A computation can be suspended in a provider or a slice. Its finally block releases
        // private state after it finishes; never clear fields out from under that operation.
        if (_compute.Wait(0))
        {
            try { Clear(); }
            finally { _compute.Release(); }
        }
    }

    private void Clear()
    {
        _pass = null;
        _computation = null;
        _report = null;
        _structure = null;
    }
}
