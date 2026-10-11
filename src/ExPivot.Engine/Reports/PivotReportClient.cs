namespace ExPivot.Engine;

/// <summary>Atomically adopts report Windows, discards obsolete replies and recovers a missing
/// Baseline Window.</summary>
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

    /// <summary>Reads the current request and, if its Baseline Window is lost, or its Window Changes do
    /// not make the source's Window, requests one complete replacement.
    /// <para><c>needed</c> is the range the grid needs, when <c>window</c> is that range read ahead
    /// (ADR-0182): a complete answer may then be shorter than <c>window</c> — a server that caps its
    /// answers — so long as its Window lies within <c>window</c> and covers <c>needed</c>, and the
    /// Window it answers is the one held. The check is made against the range before widening, as
    /// ADR-0025 makes it; an answer that does not reach it is refused by name. Null, the default,
    /// needs the whole of <c>window</c>. Window Changes and refusals always answer <c>window</c>
    /// itself.</para></summary>
    /// <returns>True if this request was adopted; false for a refusal or an obsolete reply.</returns>
    public async ValueTask<bool> ReadAsync(PivotLayout layout, PivotReportSettings settings, PivotReportWindow window,
        int maxLeaves = PivotQuery.DefaultMaxLeaves, CancellationToken cancellationToken = default, bool markChanges = true, bool refreshData = false,
        PivotReportWindow? needed = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(window);
        if (window.Start < 0 || window.Count < 0)
            throw new ArgumentOutOfRangeException(nameof(window));
        if (needed is not null && (needed.Count < 0 || !Covers(window, needed)))
            throw new ArgumentOutOfRangeException(nameof(needed), needed,
                "The range the grid needs lies within the Window asked for, which is that range read ahead (ADR-0182).");
        long generation;
        PivotReportState? previous;
        lock (_publication)
        {
            generation = ++_generation;
            previous = _current;
        }
        // The version this client holds is named whatever Window is asked: Window Changes are
        // answered only against the Window it holds, and a layout gesture lays out the data it shows
        // (ADR-0152).
        var baseline = previous?.Metadata.Version;
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
            // Memory running out is not the source's answer, nor its failure: it goes on as itself,
            // the Window held unchanged, for the Consumer to say what happened.
            catch (OutOfMemoryException) { throw; }
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
            try { issue = Validate(update, request, needed ?? request.Window, previous, out state); }
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
        PivotReportWindow needed, PivotReportState? previous, out PivotReportState? state)
    {
        state = null;
        if (update is null) return Invalid("The report response is null.");
        if (update.RequestId != request.RequestId)
            return Invalid("The report response names another request.");
        // A complete answer may be short of the rows read ahead — a server that caps its answers — but
        // it lies within the Window asked for and holds the rows the grid needs, or those would stay
        // Placeholders: the check is made against the range before widening (ADR-0182, ADR-0025).
        // Window Changes and refusals answer the Window asked for itself (ADR-0152).
        var answered = update.Window;
        if (answered != request.Window)
        {
            if (update.Rows is null || answered is null)
                return Invalid($"The report response answers {Rows(answered)} where {Rows(request.Window)} were asked for. "
                    + "Window Changes and refusals answer the Window asked for (ADR-0152).");
            if (answered.Count < 0 || !Covers(request.Window, answered) || !Covers(answered, needed))
                return Invalid($"The report response answers {Rows(answered)} where {Rows(request.Window)} were asked for "
                    + $"and the grid needs {Rows(needed)}. A source may answer fewer rows than were read ahead, but its Window "
                    + "lies within the one asked for and holds the rows the grid needs (ADR-0025, ADR-0182).");
        }
        if (update.Refusal is { } refusal) return refusal;
        if (update.Metadata is not { } metadata || string.IsNullOrEmpty(metadata.Version.Value)
            || metadata.RowCount < 0 || metadata.HeaderTierCount < 0)
            return Invalid("The report metadata is incomplete.");
        if (PivotLayoutJson.Write(metadata.Layout) != PivotLayoutJson.Write(request.Layout)
            || !PivotReportJson.SameSettings(metadata.Settings, request.Settings))
            return Invalid("The report response uses different layout or display settings.");
        var count = Math.Min(answered.Count, Math.Max(0, metadata.RowCount - answered.Start));
        PivotDisplayRow[] rows;
        if (update.Rows is { } complete)
        {
            if (update.Baseline is not null || update.Changes.Count != 0 || complete.Count != count)
                return Invalid($"The complete report Window holds {complete.Count} rows where the report has {count} in "
                    + $"{Rows(answered)}. An answer holds every row of the Window it answers, the rows the grid needs "
                    + "among them (ADR-0152, ADR-0182).");
            rows = complete.ToArray();
        }
        else
        {
            if (update.Baseline is null || update.Baseline != request.Baseline || previous is null
                || previous.Metadata.Version != update.Baseline || previous.Window != request.Window
                || previous.Rows.Count != count || previous.Metadata.RowSequenceVersion != metadata.RowSequenceVersion)
                return new(PivotReportRefusalKind.BaselineNotHeld, "The Baseline Window of the Window Changes is not the Window displayed.");
            rows = previous.Rows.ToArray();
            var changed = new HashSet<int>();
            foreach (var change in update.Changes)
            {
                if (change.Offset < 0 || change.Offset >= rows.Length || !changed.Add(change.Offset))
                    return Invalid("The Window Changes repeat or exceed a row position.");
                if (change.Row is null || !previous.Rows[change.Offset].Key.Equals(change.Row.Key))
                    return Invalid("The Window Changes change a row's identity without a new row sequence.");
                rows[change.Offset] = change.Row;
            }
        }
        var keys = new HashSet<PivotRowKey>();
        foreach (var row in rows)
        {
            if (row is null || row.Key is null || !keys.Add(row.Key)
                || row.Labels.Count != metadata.LabelColumns.Count || row.Values.Count != metadata.ValueColumns.Count
                || row.ChangedIn.Count != row.Values.Count
                || row.Role != row.Key.Role || row.ValueField != row.Key.ValueField
                || !Enum.IsDefined(row.Role) || row.RowPath.Count != row.Key.Items.Count
                || !row.RowPath.Select(p => p.Item).SequenceEqual(row.Key.Items)
                || row.Values.Any(value => value is not null && (value.Text is null || !double.IsFinite(value.Number))))
                return Invalid("The report Window contains an invalid or duplicate row.");
        }
        // What Window Changes make must be the source's Window, every row of it: changes that left
        // out a row whose shown values changed — a total, a percentage — would show its old values.
        if (update.Rows is null && update.WindowDigest is null)
            return Invalid("The Window Changes name no digest of the Window they make.");
        if (update.WindowDigest is { } digest && PivotReportDigest.Of(metadata, answered.Start, rows) != digest)
            return Invalid(update.Rows is null
                ? "The Window Changes do not make the source's Window: they leave out a changed row."
                : "The complete report Window does not match its digest.");
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
        state = new(metadata, answered, Array.AsReadOnly(rows));
        return null;
    }

    /// <summary>Whether <paramref name="range"/> lies within <paramref name="window"/>.</summary>
    private static bool Covers(PivotReportWindow window, PivotReportWindow range)
        => range.Start >= window.Start && (long)range.Start + range.Count <= (long)window.Start + window.Count;

    private static PivotReportRefusal Invalid(string message) => new(PivotReportRefusalKind.InvalidResponse, message);

    /// <summary>A Window's rows in words, for a refusal: "rows 440 to 574", or none.</summary>
    private static string Rows(PivotReportWindow? window) => window is null ? "no Window"
        : window.Count <= 0 ? $"no rows at {window.Start}"
        : $"rows {window.Start} to {(long)window.Start + window.Count - 1}";
}
