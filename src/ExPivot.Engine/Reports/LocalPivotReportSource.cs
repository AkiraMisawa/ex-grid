using System.Globalization;
using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>One report's bounded calculation and display state over a separately owned data provider.</summary>
public sealed class LocalPivotReportSource : PivotReportSource
{
    private readonly PivotSource _source;
    private readonly IReadOnlyDictionary<string, Func<object, IComparable?>> _orderKeys;
    private readonly int _versionsKept;
    private readonly TimeProvider _clock;
    private readonly PivotSlicing _slicing;
    private PivotLabelSizing _labelSizing = new();
    private readonly List<HighlightVersion> _highlight = [];
    private sealed class HighlightVersion(HeldReport held, DateTimeOffset at)
    {
        public HeldReport Held { get; } = held;
        public DateTimeOffset At { get; } = at;
        private Dictionary<PivotRowKey, PivotReportRow>? _byKey;
        public PivotReportRow? Row(PivotRowKey key, int hint)
        {
            if ((uint)hint < (uint)Held.Report.Rows.Count && Held.Report.Rows[hint].Key.Equals(key))
                return Held.Report.Rows[hint];
            return (_byKey ??= Held.Report.Rows.ToDictionary(row => row.Key)).GetValueOrDefault(key);
        }
        public Dictionary<string, int> Columns { get; } = held.Metadata.ValueColumns
            .Select((column, index) => (column.Name, index)).ToDictionary(c => c.Name, c => c.index, StringComparer.Ordinal);
    }
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Lock _published = new();
    private readonly LinkedList<HeldReport> _versions = [];
    private readonly Dictionary<PivotReportVersion, PivotReportState> _windows = [];
    private PivotComputationSession? _computation;
    private PivotOptions? _options;
    private string? _policies;
    private string? _settings;
    private string? _layout;
    private int _maxLeaves;
    private volatile bool _dirty = true;
    private bool _disposed;
    private IReadOnlyList<PivotField> _effectiveFields;
    private sealed record HeldReport(PivotReportMetadata Metadata, PivotReport Report, PivotSource Provider, IReadOnlyList<PivotField> Fields);

    internal LocalPivotReportSource(PivotSource source,
        IReadOnlyDictionary<string, Func<object, IComparable?>>? orderKeys, int versionsKept, TimeProvider clock, PivotSlicing slicing)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionsKept, 1);
        _source = source;
        _effectiveFields = source.Fields;
        _orderKeys = orderKeys ?? new Dictionary<string, Func<object, IComparable?>>();
        _versionsKept = versionsKept;
        _clock = clock;
        _slicing = slicing;
        source.Changed += SourceChanged;
    }
    /// <summary>Carries the bounded baseline from a replaced local provider into this new report
    /// computation. Its next request still recomputes from its own provider; the baseline permits
    /// exact row-order comparison and data-change highlighting across that replacement.</summary>
    public async ValueTask ContinueFromAsync(LocalPivotReportSource previous, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (ReferenceEquals(this, previous)) throw new ArgumentException("A report cannot replace itself.", nameof(previous));
        await previous._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_versions.Count != 0) throw new InvalidOperationException("A baseline must precede this report's first request.");
            _highlight.AddRange(previous._highlight);
            lock (_published)
                foreach (var held in previous._versions.Take(_versionsKept))
                {
                    _versions.AddLast(held);
                    if (previous._windows.TryGetValue(held.Metadata.Version, out var window))
                        _windows.Add(held.Metadata.Version, window);
                }
        }
        finally { previous._gate.Release(); }
    }

    /// <inheritdoc />
    public override IReadOnlyList<PivotField> Fields => _source.Fields;
    /// <inheritdoc />
    public override PivotSourceFeatures Features => _source.Features;
    /// <inheritdoc />
    public override PivotReportUpdateMode UpdateMode => _source is SnapshotPivotSource
        ? PivotReportUpdateMode.Incremental : PivotReportUpdateMode.FullRefresh;

    private void SourceChanged(PivotSourceChanged change)
    {
        _dirty = true;
        OnChanged(change);
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (request.Window.Start < 0 || request.Window.Count < 0)
            return PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.InvalidRequest, "The report Window is negative."));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var settings = PivotReportJson.Write(request.Settings);
            var layout = PivotLayoutJson.Write(request.Layout);
            if (_settings != settings && ResolveFields(request.Settings) is { } policyRefusal)
                return PivotReportUpdate.Refused(request, policyRefusal);
            var last = _versions.First?.Value;
            if (request.RefreshData || _dirty || _settings != settings || _layout != layout || _maxLeaves != request.MaxLeaves || last is null)
            {
                // Cleared before awaiting: a newer source notification keeps the next read dirty.
                var wasDirty = _dirty;
                _dirty = false;
                var policies = PivotReportJson.Write(request.Settings.OrderKeyPolicies);
                if (_computation is null || policies != _policies)
                {
                    _computation?.Dispose();
                    _computation = new(_source, _effectiveFields);
                    _policies = policies;
                }
                if (_settings != settings) _options = request.Settings.ToOptions();
                var result = await _computation.ComputeAsync(request.Layout, _options, request.MaxLeaves,
                    _slicing, cancellationToken, request.RefreshData, preferHeld: !request.MarkChanges).ConfigureAwait(false);
                if (result.IsRefused)
                {
                    _dirty = true;
                    return PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.SourceRefused, result.Refusal!.Message, result.Refusal.Field) { SourceRefusal = result.Refusal });
                }
                var report = result.Report!;
                var cube = report.Cube;
                if (!request.MarkChanges && wasDirty && last?.Metadata.SourceVersion == cube.SourceVersion) _dirty = true;
                var sameRows = last is not null && (ReferenceEquals(last.Report.Rows, report.Rows)
                    || (!result.IsReset ? !result.RowSequenceChanged
                        : await report.HasSameRowsAsAsync(last.Report, _slicing, cancellationToken).ConfigureAwait(false)));
                var widths = request.Settings.LabelMetrics is { } metrics
                    ? await _labelSizing.WidthsAsync(report, metrics, result.LabelChanges, result.RemovedRows, result.IsReset,
                        Slicer.Of(_slicing, cancellationToken)).ConfigureAwait(false) : [];
                var metadata = new PivotReportMetadata(new(Guid.NewGuid().ToString("N")), cube.SourceVersion,
                    sameRows ? last!.Metadata.RowSequenceVersion : Guid.NewGuid().ToString("N"),
                    request.Layout, request.Settings, report.Rows.Count, report.LabelColumns,
                    report.ValueColumns.Select((column, index) => new PivotDisplayColumn(column.Name, column.Header,
                        column.Role, column.ValueField, report.ColumnPath(index).Select(p => new PivotFieldItem(p.Field, p.Item)).ToArray())).ToArray(),
                    report.HeaderSpans, report.HeaderTierCount, report.ValueCaptions)
                { LabelWidths = widths };
                var next = new HeldReport(metadata, report, _source, _effectiveFields);
                RememberHighlight(next, last, request.MarkChanges);
                last = next;
                lock (_published)
                {
                    _versions.AddFirst(last);
                    while (_versions.Count > _versionsKept)
                    {
                        var evicted = _versions.Last!.Value.Metadata.Version;
                        _versions.RemoveLast();
                        _windows.Remove(evicted);
                    }
                }
                _settings = settings;
                _layout = layout;
                _maxLeaves = request.MaxLeaves;
            }
            _windows.TryGetValue(request.Baseline ?? new(""), out var previous);
            if (previous?.Window != request.Window) previous = null;
            var comparable = previous is not null && PivotReportJson.SameSettings(previous.Metadata.Settings, request.Settings)
                && PivotLayoutJson.Write(previous.Metadata.Layout) == layout;
            var priorByKey = comparable ? previous!.Rows.ToDictionary(row => row.Key) : null;
            var count = Math.Min(request.Window.Count, Math.Max(0, last.Metadata.RowCount - request.Window.Start));
            var rows = new PivotDisplayRow[count];
            var changes = new List<PivotReportRowChange>();
            PruneHighlight(_clock.GetUtcNow(), request.Settings.ChangeHighlightDuration);
            for (var i = 0; i < count; i++)
            {
                var row = last.Report.Rows[request.Window.Start + i];
                var prior = priorByKey?.GetValueOrDefault(row.Key);
                rows[i] = Project(last.Report, row, request.Window.Start + i, prior,
                    keepMarks: comparable && request.MarkChanges, previous?.Metadata.ValueColumns);
                if (previous is null || i >= previous.Rows.Count || !ReferenceEquals(previous.Rows[i], rows[i]))
                    changes.Add(new(i, rows[i]));
            }
            var state = new PivotReportState(last.Metadata, request.Window, Array.AsReadOnly(rows));
            _windows[last.Metadata.Version] = state;
            if (previous is not null && previous.Rows.Count == rows.Length
                && previous.Metadata.RowSequenceVersion == last.Metadata.RowSequenceVersion)
                return PivotReportUpdate.Delta(request, last.Metadata, changes);
            return PivotReportUpdate.Complete(request, last.Metadata, state.Rows);
        }
        catch
        {
            _dirty = true;
            throw;
        }
        finally { _gate.Release(); }
    }

    private PivotReportRefusal? ResolveFields(PivotReportSettings settings)
    {
        var fields = new List<PivotField>(Fields.Count);
        foreach (var name in settings.OrderKeyPolicies.Keys)
            if (!Fields.Any(field => field.Name == name))
                return new(PivotReportRefusalKind.UnknownOrderKeyPolicy, $"The Order Key policy names unknown field '{name}'.", name);
        foreach (var field in Fields)
        {
            if (!settings.OrderKeyPolicies.TryGetValue(field.Name, out var id)) { fields.Add(field); continue; }
            if (!_orderKeys.TryGetValue(id, out var key))
                return new(PivotReportRefusalKind.UnknownOrderKeyPolicy, $"The Order Key policy '{id}' for '{field.Name}' is not registered.", field.Name);
            fields.Add(new PivotField(field.Name, field.Type, field.Caption, field.Format, field.ItemOrder)
                { Column = field.Column, DatePart = field.DatePart, OrderKey = key });
        }
        _effectiveFields = fields;
        return null;
    }

    private PivotDisplayRow Project(PivotReport report, PivotReportRow row, int rowIndex, PivotDisplayRow? prior,
        bool keepMarks, IReadOnlyList<PivotDisplayColumn>? priorColumns)
    {
        var path = report.RowPath(row).Select(p => new PivotFieldItem(p.Field, p.Item)).ToArray();
        var values = new PivotDisplayValue?[report.ValueColumns.Count];
        var changed = new DateTimeOffset?[values.Length];
        var same = prior is not null && prior.Role == row.Role && prior.ValueField == row.ValueField
            && prior.CarriesValues == row.CarriesValues && prior.Labels.SequenceEqual(row.Labels) && prior.Values.Count == values.Length
            && prior.RowPath.Select(p => (p.Field, p.Item.Kind, p.Item.Value))
                .SequenceEqual(path.Select(p => (p.Field, p.Item.Kind, p.Item.Value)));
        for (var c = 0; c < values.Length; c++)
        {
            values[c] = PivotDisplayValue.From(report.ValueAt(row, c));
            changed[c] = ChangedAt(row.Key, rowIndex, report.ValueColumns[c].Name);
            var oldColumn = -1;
            if (priorColumns is not null)
                for (var j = 0; j < priorColumns.Count; j++)
                    if (priorColumns[j].Name == report.ValueColumns[c].Name) { oldColumn = j; break; }
            if (prior is not null && oldColumn >= 0 && oldColumn < prior.Values.Count)
            {
                changed[c] ??= keepMarks ? prior.ChangedAt[oldColumn] : null;
                same &= oldColumn == c && prior.Values[oldColumn] == values[c] && prior.ChangedAt[oldColumn] == changed[c];
            }
        }
        if (same) return prior!;
        return new(row.Key, row.Role, row.ValueField, row.CarriesValues, row.Labels, values,
            path, changed);
    }

    // Retain only the data versions whose marks can still be shown, plus their baseline.
    // Versions share unchanged calculation state and point to no predecessor. Display rows own
    // only their resolved timestamps, so grid paint history cannot extend this lifetime.
    private void RememberHighlight(HeldReport next, HeldReport? previous, bool markChanges)
    {
        var at = _clock.GetUtcNow();
        if (!markChanges || previous is null
            || PivotLayoutJson.Write(previous.Metadata.Layout) != PivotLayoutJson.Write(next.Metadata.Layout)
            || !PivotReportJson.SameSettings(previous.Metadata.Settings, next.Metadata.Settings))
            _highlight.Clear();
        else if (previous.Metadata.SourceVersion == next.Metadata.SourceVersion && _highlight.Count > 0)
        {
            _highlight[0] = new(next, _highlight[0].At);
            return;
        }
        _highlight.Insert(0, new(next, at));
        PruneHighlight(at, next.Metadata.Settings.ChangeHighlightDuration);
    }

    private void PruneHighlight(DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            if (_highlight.Count > 1) _highlight.RemoveRange(1, _highlight.Count - 1);
            return;
        }
        for (var i = 0; i < _highlight.Count - 1; i++)
            if (now - _highlight[i].At >= duration)
            {
                _highlight.RemoveRange(i + 1, _highlight.Count - i - 1);
                break;
            }
    }

    private DateTimeOffset? ChangedAt(PivotRowKey key, int rowIndex, string column)
    {
        for (var i = 0; i + 1 < _highlight.Count; i++)
        {
            var newer = _highlight[i];
            var older = _highlight[i + 1];
            var newRow = newer.Row(key, rowIndex);
            if (newRow is null || !newer.Columns.TryGetValue(column, out var nc)) continue;
            var oldRow = older.Row(key, rowIndex);
            if (oldRow is null || !older.Columns.TryGetValue(column, out var oc)
                || (newer.Held.Report.ValueAt(newRow, nc)?.Text ?? "") != (older.Held.Report.ValueAt(oldRow, oc)?.Text ?? ""))
                return newer.At;
        }
        return null;
    }

    // A versioned operation captures one immutable report. It neither waits for a successor's
    // calculation nor keeps the publication lock while yielding or asking the data provider.
    private HeldReport? Held(PivotReportVersion version)
    {
        lock (_published)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _versions.FirstOrDefault(value => value.Metadata.Version == version);
        }
    }
    private static PivotReportRefusal NotHeld(PivotReportVersion version)
        => new(PivotReportRefusalKind.ReportVersionNotHeld, $"Report Version '{version.Value}' is no longer held.");
    /// <inheritdoc />
    public override ValueTask<PivotItemPage> RawItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
        => _source.ItemsAsync(query, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<PivotReportItemsResult> ItemsAsync(PivotReportItemsQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var held = Held(query.Version);
        if (held is null) return new(query.Version, "", [], 0, NotHeld(query.Version));
        var field = held.Fields.FirstOrDefault(field => field.Name == query.Field);
        if (field is null || query.Max < 0)
            return new(query.Version, held.Metadata.SourceVersion, [], 0,
                new(PivotReportRefusalKind.InvalidRequest, "Items needs a declared field and nonnegative cap."));
        var page = await held.Provider.ItemsAsync(new(query.Field, held.Metadata.SourceVersion, query.Search, query.Max),
            cancellationToken).ConfigureAwait(false);
        if (page.IsRefused)
            return new(query.Version, held.Metadata.SourceVersion, [], 0,
                new(PivotReportRefusalKind.SourceRefused, page.Refusal!.Message) { SourceRefusal = page.Refusal });
        return new(query.Version, page.SourceVersion,
            PivotEngine.ItemsOf(page, held.Report.Layout, field, held.Report.Options), page.Total);
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var held = Held(query.Version);
        if (held is null) return new(query.Version, null, NotHeld(query.Version));
        var row = held.Report.Rows.FirstOrDefault(row => row.Key.Equals(query.Row));
        if (row is null || query.ValueColumn < -1 || query.ValueColumn >= held.Report.ValueColumns.Count)
            return new(query.Version, null, new(PivotReportRefusalKind.InvalidRequest, "The requested cell is not in this report."));
        var page = await held.Provider.DetailsAsync(held.Report.DetailsQuery(row, query.ValueColumn, query.Start, query.Count), cancellationToken).ConfigureAwait(false);
        return new(query.Version, page);
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var held = Held(query.Version);
        if (held is null) return new(query.Version, [], NotHeld(query.Version));
        var issue = CheckRanges(held.Report, query.Columns, query.Ranges, query.MaxCells);
        if (issue is not null) return new(query.Version, [], issue);
        var columns = Columns(held.Report, query.Columns);
        var blocks = new List<PivotReportCopyBlock>();
        var pace = Slicer.Of(_slicing, cancellationToken);
        foreach (var range in query.Ranges)
        {
            var rows = new List<IReadOnlyList<PivotReportCopyCell>>();
            for (var r = range.Top; r <= range.Bottom; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cells = new List<PivotReportCopyCell>();
                for (var c = range.Left; c <= range.Right; c++)
                {
                    var (value, index, _) = columns[c];
                    if (!value)
                    {
                        var label = held.Report.Rows[r].Labels[index].Text;
                        cells.Add(new(label ?? "", label, false));
                    }
                    else
                    {
                        var cell = held.Report.ValueAt(held.Report.Rows[r], index);
                        cells.Add(new(cell?.Text ?? "", cell?.ToString(null, CultureInfo.InvariantCulture), cell is { IsError: false }));
                    }
                }
                rows.Add(cells);
                if (pace.Done(cells.Count)) await pace.PauseAsync().ConfigureAwait(false);
            }
            blocks.Add(new(columns.Skip(range.Left).Take(range.Right - range.Left + 1).Select(c => c.Header).ToArray(), rows));
        }
        return new(query.Version, blocks);
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var held = Held(query.Version);
        if (held is null) return new(query.Version, default, default, default, false, null, "", NotHeld(query.Version));
        var issue = CheckRanges(held.Report, query.Columns, query.Ranges, long.MaxValue);
        if (issue is not null) return new(query.Version, default, default, default, false, null, "", issue);
        var columns = Columns(held.Report, query.Columns);
        var counts = new AggregateCounts();
        var sum = new AggregateSum();
        var extremes = new AggregateExtremes();
        var error = false;
        var pace = Slicer.Of(_slicing, cancellationToken);
        foreach (var (r, c) in SelectedCells(query.Ranges))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pace.Done(1)) await pace.PauseAsync().ConfigureAwait(false);
            var (isValue, index, _) = columns[c];
            if (!isValue)
            {
                if (!string.IsNullOrEmpty(held.Report.Rows[r].Labels[index].Text)) counts.Values++;
                continue;
            }
            var value = held.Report.ValueAt(held.Report.Rows[r], index);
            if (value is null) continue;
            counts.Values++;
            if (value.IsError) { error = true; continue; }
            var first = counts.Numbers == 0;
            if (value.Exact is { } exact)
            {
                AggregateArithmetic.Add(ref sum, exact, first);
                AggregateArithmetic.Add(ref extremes, exact, first);
            }
            else
            {
                AggregateArithmetic.Add(ref sum, value.Number, first);
                AggregateArithmetic.Add(ref extremes, value.Number, first);
            }
            counts.Numbers++;
        }
        AggregateArithmetic.Finish(ref sum);
        AggregateArithmetic.Finish(ref extremes);
        string? format = null;
        if (query.FocusRow is { } fr && fr >= 0 && fr < held.Report.Rows.Count
            && query.FocusColumn is { } fc && fc >= 0 && fc < columns.Length && columns[fc].Value)
        {
            var vf = held.Report.ValueFieldAt(held.Report.Rows[fr], columns[fc].Index);
            if (vf >= 0)
            {
                var field = held.Report.Layout.Values[vf];
                format = field.NumberFormat ?? (field.ShowValuesAs != PivotShowValuesAs.NoCalculation ? "0.00%" : "G15");
            }
        }
        return new(query.Version, counts, sum, extremes, error, format, held.Metadata.Culture.Name);
    }

    // The union is represented by row bands and merged column intervals, not one stored
    // entry per selected cell. Selecting a large report does not allocate its cell count.
    private static IEnumerable<(int Row, int Column)> SelectedCells(IReadOnlyList<PivotReportRange> ranges)
    {
        var boundaries = ranges.SelectMany(r => new[] { r.Top, r.Bottom + 1 }).Distinct().Order().ToArray();
        for (var b = 0; b + 1 < boundaries.Length; b++)
        {
            var top = boundaries[b];
            var intervals = ranges.Where(r => r.Top <= top && r.Bottom >= top).OrderBy(r => r.Left).ToArray();
            var merged = new List<(int Left, int Right)>();
            foreach (var interval in intervals)
            {
                if (merged.Count > 0 && interval.Left <= (long)merged[^1].Right + 1)
                    merged[^1] = (merged[^1].Left, Math.Max(merged[^1].Right, interval.Right));
                else merged.Add((interval.Left, interval.Right));
            }
            for (var r = top; r < boundaries[b + 1]; r++)
                foreach (var (left, right) in merged)
                    for (var c = left; c <= right; c++)
                        yield return (r, c);
        }
    }

    private static PivotReportRefusal? CheckRanges(PivotReport report, IReadOnlyList<string> names,
        IReadOnlyList<PivotReportRange> ranges, long maxCells)
    {
        var known = report.LabelColumns.Select(c => c.Name).Concat(report.ValueColumns.Select(c => c.Name)).ToHashSet(StringComparer.Ordinal);
        if (names.Any(name => !known.Contains(name)) || names.Distinct(StringComparer.Ordinal).Count() != names.Count)
            return new(PivotReportRefusalKind.InvalidRequest, "The selection names an unknown or repeated column.");
        long count = 0;
        foreach (var r in ranges)
        {
            if (r.Top < 0 || r.Left < 0 || r.Bottom < r.Top || r.Right < r.Left || r.Bottom >= report.Rows.Count || r.Right >= names.Count)
                return new(PivotReportRefusalKind.InvalidRequest, "The selection is outside the requested report.");
            var cells = ((long)r.Bottom - r.Top + 1) * ((long)r.Right - r.Left + 1);
            if (cells > maxCells - count)
                return new(PivotReportRefusalKind.InvalidRequest, $"Copy exceeds the limit of {maxCells} cells.");
            count += cells;
        }
        return null;
    }
    private static (bool Value, int Index, string Header)[] Columns(PivotReport report, IReadOnlyList<string> names)
    {
        var all = report.LabelColumns.Select((c, i) => (c.Name, Value: false, Index: i, c.Header))
            .Concat(report.ValueColumns.Select((c, i) => (c.Name, Value: true, Index: i, c.Header)))
            .ToDictionary(c => c.Name, c => (c.Value, c.Index, c.Header), StringComparer.Ordinal);
        return names.Select(name => all[name]).ToArray();
    }
    /// <inheritdoc />
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default) => _source.RefreshAsync(cancellationToken);
    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            lock (_published)
            {
                _disposed = true;
                _versions.Clear();
            }
            _source.Changed -= SourceChanged;
            _windows.Clear();
            _highlight.Clear();
            _labelSizing = new();
            _computation?.Dispose();
            _computation = null;
        }
        finally { _gate.Release(); }
    }
}
