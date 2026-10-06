namespace ExPivot.Engine;

/// <summary>Atomically adopts report Windows, discards obsolete replies and recovers a missing baseline.</summary>
public sealed class PivotReportClient
{
    private readonly PivotReportSource _source;
    private long _generation;
    private readonly Lock _publication = new();
    private PivotReportState? _current;
    private PivotReportRefusal? _refusal;
    /// <summary>Creates the state owner for one report view; source lifetime remains the Consumer's.
    /// A previously validated Window may be carried across a source replacement as its baseline.</summary>
    public PivotReportClient(PivotReportSource source, PivotReportState? previous = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _current = previous;
    }
    /// <summary>The last completely validated Window; a failed request leaves it unchanged.</summary>
    public PivotReportState? Current { get { lock (_publication) return _current; } }
    /// <summary>The latest current request's refusal, or null after a successful adoption.</summary>
    public PivotReportRefusal? Refusal { get { lock (_publication) return _refusal; } }

    /// <summary>Reads the current request and, if its baseline is lost, requests one complete replacement.</summary>
    /// <returns>True if this request was adopted; false for a refusal or an obsolete reply.</returns>
    public async ValueTask<bool> ReadAsync(PivotLayout layout, PivotReportSettings settings, PivotReportWindow window,
        int maxLeaves = PivotQuery.DefaultMaxLeaves, CancellationToken cancellationToken = default, bool markChanges = true, bool refreshData = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(window);
        if (window.Start < 0 || window.Count < 0)
            throw new ArgumentOutOfRangeException(nameof(window));
        long generation;
        PivotReportState? previous;
        lock (_publication)
        {
            generation = ++_generation;
            previous = _current;
        }
        var baseline = previous?.Window == window ? previous.Metadata.Version : null;
        var request = new PivotReportRequest(Guid.NewGuid().ToString("N"), layout, settings, window, baseline, maxLeaves)
            { MarkChanges = markChanges, RefreshData = refreshData };
        var recovered = false;
        while (true)
        {
            PivotReportUpdate update;
            try
            {
                update = await _source.WindowAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                lock (_publication)
                {
                    if (generation != _generation) return false;
                    _refusal = new(PivotReportRefusalKind.StaleReport, error.Message);
                    return false;
                }
            }
            lock (_publication)
                if (generation != _generation) return false;
            cancellationToken.ThrowIfCancellationRequested();
            PivotReportRefusal? issue;
            PivotReportState? state;
            try { issue = Validate(update, request, previous, out state); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                issue = Invalid("The report response is malformed: " + error.Message);
                state = null;
            }
            // Validation can run concurrently with a newer request. Adopting either its
            // state or refusal must be serialized with starting that request (ADR-0152).
            lock (_publication)
            {
                if (generation != _generation) return false;
                cancellationToken.ThrowIfCancellationRequested();
                if (issue is null)
                {
                    _current = state;
                    _refusal = null;
                    return true;
                }
                if (!recovered && request.Baseline is not null
                    && issue.Kind is PivotReportRefusalKind.BaselineNotHeld or PivotReportRefusalKind.InvalidResponse)
                {
                    recovered = true;
                    request = request with { RequestId = Guid.NewGuid().ToString("N"), Baseline = null };
                    continue;
                }
                _refusal = recovered ? new(PivotReportRefusalKind.StaleReport, issue.Message, issue.Field) : issue;
                return false;
            }
        }
    }

    private static PivotReportRefusal? Validate(PivotReportUpdate update, PivotReportRequest request,
        PivotReportState? previous, out PivotReportState? state)
    {
        state = null;
        if (update is null) return Invalid("The report response is null.");
        if (update.RequestId != request.RequestId || update.Window != request.Window)
            return Invalid("The report response names another request or Window.");
        if (update.Refusal is { } refusal) return refusal;
        if (update.Metadata is not { } metadata || string.IsNullOrEmpty(metadata.Version.Value)
            || metadata.RowCount < 0 || metadata.HeaderTierCount < 0)
            return Invalid("The report metadata is incomplete.");
        if (PivotLayoutJson.Write(metadata.Layout) != PivotLayoutJson.Write(request.Layout)
            || !PivotReportJson.SameSettings(metadata.Settings, request.Settings))
            return Invalid("The report response uses different layout or display settings.");
        var count = Math.Min(request.Window.Count, Math.Max(0, metadata.RowCount - request.Window.Start));
        PivotDisplayRow[] rows;
        if (update.Rows is { } complete)
        {
            if (update.Baseline is not null || update.Changes.Count != 0 || complete.Count != count)
                return Invalid("The complete report Window has a different extent.");
            rows = complete.ToArray();
        }
        else
        {
            if (update.Baseline is null || update.Baseline != request.Baseline || previous is null
                || previous.Metadata.Version != update.Baseline || previous.Window != request.Window
                || previous.Rows.Count != count || previous.Metadata.RowSequenceVersion != metadata.RowSequenceVersion)
                return new(PivotReportRefusalKind.BaselineNotHeld, "The delta baseline is not the displayed Window.");
            rows = previous.Rows.ToArray();
            var changed = new HashSet<int>();
            foreach (var change in update.Changes)
            {
                if (change.Offset < 0 || change.Offset >= rows.Length || !changed.Add(change.Offset))
                    return Invalid("The report delta repeats or exceeds a row position.");
                if (change.Row is null || !previous.Rows[change.Offset].Key.Equals(change.Row.Key))
                    return Invalid("A delta changes row identity without a new row sequence.");
                rows[change.Offset] = change.Row;
            }
        }
        var keys = new HashSet<PivotRowKey>();
        foreach (var row in rows)
        {
            if (row is null || row.Key is null || !keys.Add(row.Key)
                || row.Labels.Count != metadata.LabelColumns.Count || row.Values.Count != metadata.ValueColumns.Count
                || row.ChangedAt.Count != row.Values.Count || row.Role != row.Key.Role || row.ValueField != row.Key.ValueField
                || !Enum.IsDefined(row.Role) || row.RowPath.Count != row.Key.Items.Count
                || !row.RowPath.Select(p => p.Item).SequenceEqual(row.Key.Items)
                || row.Values.Any(value => value is not null && (value.Text is null || !double.IsFinite(value.Number))))
                return Invalid("The report Window contains an invalid or duplicate row.");
        }
        if (metadata.LabelWidths.Count != 0 && metadata.LabelWidths.Count != metadata.LabelColumns.Count
            || metadata.LabelWidths.Any(width => !double.IsFinite(width) || width < 0))
            return Invalid("The report label widths do not match its columns.");
        if (previous is not null && previous.Metadata.Version == metadata.Version
            && previous.Metadata.SourceVersion != metadata.SourceVersion)
            return Invalid("One Report Version names two Source Versions.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in metadata.LabelColumns.Select(c => c.Name).Concat(metadata.ValueColumns.Select(c => c.Name)))
            if (string.IsNullOrEmpty(name) || !names.Add(name)) return Invalid("The report has duplicate column names.");
        foreach (var span in metadata.HeaderSpans)
            if (span.FirstColumn < 0 || span.ColumnCount <= 0 || (long)span.FirstColumn + span.ColumnCount > metadata.ValueColumns.Count
                || span.Tier < 1 || span.TierSpan < 1 || span.Tier > metadata.HeaderTierCount || span.TierSpan > span.Tier)
                return Invalid("The report has an invalid header span.");
        state = new(metadata, request.Window, Array.AsReadOnly(rows));
        return null;
    }

    private static PivotReportRefusal Invalid(string message) => new(PivotReportRefusalKind.InvalidResponse, message);
}
