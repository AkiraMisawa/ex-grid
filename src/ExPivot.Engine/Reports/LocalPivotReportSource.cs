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
    private readonly PivotLabelSizing _labelSizing = new();
    private readonly SemaphoreSlim _gate = new(1);
    private readonly LinkedList<HeldReport> _versions = [];
    private readonly Dictionary<PivotReportVersion, PivotReportState> _windows = [];
    private string? _settings;
    private string? _layout;
    private int _maxLeaves;
    private volatile bool _dirty = true;
    private bool _disposed;
    private IReadOnlyList<PivotField> _effectiveFields;
    private sealed record HeldReport(PivotReportMetadata Metadata, PivotReport Report);

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
            var settings = PivotReportJson.Write(request.Settings);
            var layout = PivotLayoutJson.Write(request.Layout);
            if (_settings != settings && ResolveFields(request.Settings) is { } policyRefusal)
                return PivotReportUpdate.Refused(request, policyRefusal);
            var last = _versions.First?.Value;
            if (_dirty || _settings != settings || _layout != layout || _maxLeaves != request.MaxLeaves || last is null)
            {
                // Cleared before awaiting: a newer source notification keeps the next read dirty.
                _dirty = false;
                var query = PivotQuery.For(request.Layout, request.MaxLeaves);
                var answer = await _source.AggregateAsync(query, cancellationToken).ConfigureAwait(false);
                if (answer.IsRefused)
                {
                    _dirty = true;
                    return PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.SourceRefused, answer.Refusal!.Message, answer.Refusal.Field));
                }
                var cube = await PivotEngine.CubeAsync(query, answer, _effectiveFields, slicing: _slicing, cancellationToken: cancellationToken).ConfigureAwait(false);
                var report = await PivotEngine.ReportAsync(cube, request.Layout, request.Settings.ToOptions(), slicing: _slicing, cancellationToken: cancellationToken).ConfigureAwait(false);
                var sameRows = last is not null && await report.HasSameRowsAsAsync(last.Report, cancellationToken: cancellationToken).ConfigureAwait(false);
                var metadata = new PivotReportMetadata(new(Guid.NewGuid().ToString("N")), cube.SourceVersion,
                    sameRows ? last!.Metadata.RowSequenceVersion : Guid.NewGuid().ToString("N"),
                    request.Layout, request.Settings, report.Rows.Count, report.LabelColumns,
                    report.ValueColumns.Select((column, index) => new PivotDisplayColumn(column.Name, column.Header,
                        column.Role, column.ValueField, report.ColumnPath(index).Select(p => new PivotFieldItem(p.Field, p.Item)).ToArray())).ToArray(),
                    report.HeaderSpans, report.HeaderTierCount, report.ValueCaptions)
                { LabelWidths = request.Settings.LabelMetrics is { } metrics ? _labelSizing.Widths(report, metrics, [], [], true) : [] };
                last = new(metadata, report);
                _versions.AddFirst(last);
                while (_versions.Count > _versionsKept)
                {
                    var evicted = _versions.Last!.Value.Metadata.Version;
                    _versions.RemoveLast();
                    _windows.Remove(evicted);
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
            var now = _clock.GetUtcNow();
            for (var i = 0; i < count; i++)
            {
                var row = last.Report.Rows[request.Window.Start + i];
                var prior = priorByKey?.GetValueOrDefault(row.Key);
                rows[i] = Project(last.Report, row, prior, now);
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

    private static PivotDisplayRow Project(PivotReport report, PivotReportRow row, PivotDisplayRow? prior, DateTimeOffset now)
    {
        var values = new PivotDisplayValue?[report.ValueColumns.Count];
        var changed = new DateTimeOffset?[values.Length];
        var same = prior is not null && prior.Role == row.Role && prior.ValueField == row.ValueField
            && prior.CarriesValues == row.CarriesValues && prior.Labels.SequenceEqual(row.Labels) && prior.Values.Count == values.Length;
        for (var c = 0; c < values.Length; c++)
        {
            values[c] = PivotDisplayValue.From(row.ValueAt(c));
            if (prior is not null && c < prior.Values.Count)
            {
                changed[c] = prior.Values[c]?.Text == values[c]?.Text ? prior.ChangedAt[c] : now;
                same &= prior.Values[c] == values[c];
            }
        }
        if (same) return prior!;
        return new(row.Key, row.Role, row.ValueField, row.CarriesValues, row.Labels, values,
            report.RowPath(row).Select(p => new PivotFieldItem(p.Field, p.Item)).ToArray(), changed);
    }

    private HeldReport? Held(PivotReportVersion version) => _versions.FirstOrDefault(value => value.Metadata.Version == version);
    private static PivotReportRefusal NotHeld(PivotReportVersion version)
        => new(PivotReportRefusalKind.ReportVersionNotHeld, $"Report Version '{version.Value}' is no longer held.");
    /// <inheritdoc />
    public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
        => _source.ItemsAsync(query, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var held = Held(query.Version);
            if (held is null) return new(query.Version, null, NotHeld(query.Version));
            var row = held.Report.Rows.FirstOrDefault(row => row.Key.Equals(query.Row));
            if (row is null || query.ValueColumn < -1 || query.ValueColumn >= held.Report.ValueColumns.Count)
                return new(query.Version, null, new(PivotReportRefusalKind.InvalidRequest, "The requested cell is not in this report."));
            var page = await _source.DetailsAsync(held.Report.DetailsQuery(row, query.ValueColumn, query.Start, query.Count), cancellationToken).ConfigureAwait(false);
            return new(query.Version, page);
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var held = Held(query.Version);
            if (held is null) return new(query.Version, [], NotHeld(query.Version));
            var issue = CheckRanges(held.Report, query.Columns, query.Ranges, query.MaxCells);
            if (issue is not null) return new(query.Version, [], issue);
            var columns = Columns(held.Report, query.Columns);
            var blocks = new List<PivotReportCopyBlock>();
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
                            var cell = held.Report.Rows[r].ValueAt(index);
                            cells.Add(new(cell?.Text ?? "", cell?.ToString(null, CultureInfo.InvariantCulture), cell is { IsError: false }));
                        }
                    }
                    rows.Add(cells);
                }
                blocks.Add(new(columns.Skip(range.Left).Take(range.Right - range.Left + 1).Select(c => c.Header).ToArray(), rows));
            }
            return new(query.Version, blocks);
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc />
    public override async ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var held = Held(query.Version);
            if (held is null) return new(query.Version, default, default, default, false, null, "", NotHeld(query.Version));
            var issue = CheckRanges(held.Report, query.Columns, query.Ranges, long.MaxValue);
            if (issue is not null) return new(query.Version, default, default, default, false, null, "", issue);
            var columns = Columns(held.Report, query.Columns);
            var counts = new AggregateCounts();
            var sum = new AggregateSum();
            var extremes = new AggregateExtremes();
            var error = false;
            foreach (var (r, c) in SelectedCells(query.Ranges))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (isValue, index, _) = columns[c];
                if (!isValue)
                {
                    if (!string.IsNullOrEmpty(held.Report.Rows[r].Labels[index].Text)) counts.Values++;
                    continue;
                }
                var value = held.Report.Rows[r].ValueAt(index);
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
        finally { _gate.Release(); }
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
    public override ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _source.Changed -= SourceChanged;
            _versions.Clear();
            _windows.Clear();
        }
        return ValueTask.CompletedTask;
    }
}
