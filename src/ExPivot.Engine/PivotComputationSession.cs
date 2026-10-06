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

    private void ProviderChanged(PivotSourceChanged change) => Interlocked.Increment(ref _sourceEpoch);

    private void Changed(SnapshotChange change)
    {
        lock (_gate)
            if (!_disposed)
                _pending.Add(change);
    }

    /// <summary>Computes the latest complete report under the requested layout and display settings.</summary>
    public async ValueTask<PivotComputationResult> ComputeAsync(PivotLayout layout, PivotOptions? options = null,
        int maxLeaves = 200_000, PivotSlicing? slicing = null, CancellationToken cancellationToken = default,
        bool refreshData = false, bool preferHeld = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _compute.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var query = PivotQuery.For(layout, maxLeaves);
            var sourceEpoch = Volatile.Read(ref _sourceEpoch);
            if (_source.Refusal(query) is { } refusal)
                return new(null, refusal, false);
            var chosen = options ?? PivotOptions.Default;
            var layoutName = PivotLayoutJson.Write(layout);
            var pace = Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken);
            var reset = _report is null;
            var updated = false;
            var answered = false;
            PivotCube cube;
            if (preferHeld && _report is not null && _report.Cube.Holds(layout) && _report.Cube.Query.MaxLeaves == maxLeaves)
                cube = _report.Cube;
            else if (_source is SnapshotPivotSource source)
            {
                answered = true;
                var captured = source.Capture();
                SnapshotChange[] changes;
                lock (_gate)
                    changes = _pending.Where(c => c.After.Version <= captured.Snapshot.Version).OrderBy(c => c.After.Version).ToArray();
                reset |= _pass is null || !_pass.Query.Equals(query);
                if (!reset)
                {
                    var leaves = new HashSet<int>();
                    var items = _pass!.Axes.Select(_ => new HashSet<int>()).ToArray();
                    foreach (var change in changes)
                    {
                        if (change.After.Version <= _pass!.Snapshot.Version)
                            continue;
                        if (!_pass.Fold(change))
                        {
                            reset = true;
                            break;
                        }
                        updated = true;
                        leaves.UnionWith(_pass.ChangedLeaves);
                        for (var i = 0; i < items.Length; i++) items[i].UnionWith(_pass.Axes[i].ChangedItems);
                    }
                    _pass.ChangedLeaves.UnionWith(leaves);
                    for (var i = 0; i < items.Length; i++) _pass.Axes[i].ChangedItems.UnionWith(items[i]);
                }
                if (reset)
                {
                    var bindings = FieldBinding.Of(captured.Snapshot, source.Fields).Item2;
                    var pass = new AggregationPass(captured.Snapshot, bindings, query,
                        captured.Snapshot.RecordKey is not null, (slicing ?? PivotSlicing.Default).RowsRead);
                    await pace.PassAsync(pass.RowCount, pass.Step).ConfigureAwait(false);
                    await pass.CompleteAsync(pace).ConfigureAwait(false);
                    var answer = await pass.AnswerAsync(captured.Version, pace).ConfigureAwait(false);
                    if (answer.IsRefused)
                        return new(null, answer.Refusal, true);
                    cube = await PivotEngine.CubeAsync(query, answer, _fields, slicing, cancellationToken).ConfigureAwait(false);
                    _pass = pass;
                    _computation = await ComputationCube.CreateAsync(cube, pass, pace).ConfigureAwait(false);
                }
                else if (updated)
                    cube = await _computation!.UpdateAsync(_pass!, source.VersionOf(_pass!.Snapshot), pace).ConfigureAwait(false);
                else cube = _report!.Cube;
                source.RememberComputation(_pass!.Snapshot);
                lock (_gate)
                    _pending.RemoveAll(c => c.After.Version <= _pass.Snapshot.Version);
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
                    reset = true;
                }
            }
            var relayout = reset || _layout != layoutName || !Equals(_options, chosen);
            if (relayout)
            {
                var report = await PivotEngine.ReportAsync(cube, layout, chosen, slicing, cancellationToken).ConfigureAwait(false);
                var structure = new ComputationReport();
                report = await structure.InitializeAsync(report, pace).ConfigureAwait(false);
                _report = report;
                _structure = structure;
            }
            else if (updated)
                _report = await _structure!.UpdateAsync(_report!, cube, _computation!, pace).ConfigureAwait(false);
            _layout = layoutName;
            _options = chosen;
            if (answered) _computedEpoch = sourceEpoch;
            return new(_report, null, relayout, labels: updated && !relayout ? _structure!.LabelChanges.ToArray() : [],
                removed: updated && !relayout ? _structure!.RemovedRows.ToArray() : [])
            { RowSequenceChanged = relayout || (updated && _structure!.RowSequenceChanged) };
        }
        catch
        {
            // Private working state may have advanced before cancellation; no published value
            // was mutated. The next explicit asking initializes a complete new computation.
            _pass = null;
            _computation = null;
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
