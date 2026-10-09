namespace ExPivot.Engine;

/// <summary>A report source: the asynchronous report boundary shared by a local browser and a remote
/// server (ADR-0151). It computes a Pivot Report over a Pivot Source and answers the Window asked for,
/// in full or as Window Changes, and the operations versioned by it.</summary>
public abstract class PivotReportSource : IAsyncDisposable
{
    /// <summary>Fields offered to the Field List.</summary>
    public abstract IReadOnlyList<PivotField> Fields { get; }
    /// <summary>Offered Aggregations and Refresh capability.</summary>
    public abstract PivotSourceFeatures Features { get; }
    /// <summary>Whether changed contributions can be calculated incrementally, or the Pivot Source
    /// recomputes in full — declared, never claimed silently (ADR-0153). A component answers a
    /// full-refresh source's notice of newer data by asking it to refresh
    /// (<see cref="PivotReportRequest.RefreshData"/>).</summary>
    public abstract PivotReportUpdateMode UpdateMode { get; }
    /// <summary>A complete report Window, or Window Changes from the Baseline Window the request
    /// names.</summary>
    public abstract ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default);
    /// <summary>A field's Items at the requested Source Version.</summary>
    public abstract ValueTask<PivotItemPage> RawItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default);
    /// <summary>Items labeled and ordered in the report computation process.</summary>
    public abstract ValueTask<PivotReportItemsResult> ItemsAsync(PivotReportItemsQuery query, CancellationToken cancellationToken = default);
    /// <summary>All selected cells at the requested Report Version, including offscreen cells.</summary>
    public abstract ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default);
    /// <summary>The selected cells' summary at the requested Report Version.</summary>
    public abstract ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default);
    /// <summary>
    /// A page of the Source Records behind a cell — Show Details — under the Source Version the
    /// report showing it was computed from (ADR-0151: Details refers to the Source Version, not to a
    /// Report Version). The question is resolved from the cell the user acted on
    /// (<see cref="PivotReportMetadata.DetailsQuery"/>), so it holds whatever layouts follow; it is
    /// answered while the Pivot Source still holds that Source Version, and refused by it
    /// (<see cref="PivotSourceRefusalKind.SourceVersionNotHeld"/>) once it does not — never answered
    /// from newer data.
    /// </summary>
    /// <param name="query">The cell's Items, the Hidden Items, the range of records, and the Source Version.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    public abstract ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default);
    /// <summary>Asks the Pivot Source to refresh.</summary>
    public abstract ValueTask RefreshAsync(CancellationToken cancellationToken = default);
    /// <summary>Releases this report's calculation state; it does not own the Pivot Source it computes over.</summary>
    public abstract ValueTask DisposeAsync();
    /// <summary>The Pivot Source learned that its data changed.</summary>
    public event Action<PivotSourceChanged>? Changed;
    /// <summary>Raises the data-change notification.</summary>
    protected void OnChanged(PivotSourceChanged change) => Changed?.Invoke(change);

    /// <summary>Runs a report over a Pivot Source: in the browser over local data, or on a server
    /// behind the Consumer's transport.</summary>
    /// <param name="source">The Pivot Source; this report does not own its lifetime.</param>
    /// <param name="orderKeys">Server-registered Order Key functions, by policy identifier.</param>
    /// <param name="versionsKept">Bounded number of immutable reports available to versioned operations.
    /// The reports the Change Highlight compares are kept as well: every one published less than the
    /// request's <see cref="PivotReportSettings.ChangeHighlightDuration"/> before the newest, and the
    /// one before them — at most max(versionsKept, ⌈ChangeHighlightDuration ÷ the time between the
    /// reports it publishes⌉ + 1) in all (ADR-0153).</param>
    /// <param name="slicing">How calculation work shares the calling thread.</param>
    /// <param name="timeProvider">The clock that timestamps shown-text changes.</param>
    public static LocalPivotReportSource From(PivotSource source,
        IReadOnlyDictionary<string, Func<object, IComparable?>>? orderKeys = null,
        int versionsKept = 2, TimeProvider? timeProvider = null, PivotSlicing? slicing = null)
        => new(source, orderKeys, versionsKept, timeProvider ?? TimeProvider.System, slicing ?? PivotSlicing.Default);

    /// <summary>Uses the Consumer's transport. Authentication, connection and server report lifetime remain the Consumer's.</summary>
    /// <param name="fields">The fields the server's report offers.</param>
    /// <param name="features">Its Aggregations and whether it refreshes.</param>
    /// <param name="updateMode">How the server's Pivot Source learns of changes.</param>
    /// <param name="window">Asks the server for a Window: a complete one, or Window Changes from the
    /// request's Baseline Window. Window Changes replace every row of the Window whose shown values
    /// changed — subtotal and grand total rows, and rows whose percentage changed, included — and
    /// name the digest of the whole Window they make (<see cref="PivotReportUpdate.WindowDigest"/>).
    /// A server running <see cref="LocalPivotReportSource"/> builds them so by construction; a
    /// transport that coalesces, filters or builds Window Changes itself must keep that promise,
    /// computing the digest from its own complete Window. Window Changes that do not reproduce their
    /// digest are never shown: a complete Window is asked for in their place.</param>
    /// <param name="items">Answers a field's Items at a Source Version.</param>
    /// <param name="copy">Answers a versioned Copy.</param>
    /// <param name="summary">Answers a versioned Selection Summary.</param>
    /// <param name="details">Answers a page of the Source Records behind a cell, under the Source
    /// Version the question names (ADR-0151).</param>
    /// <param name="refresh">Asks the server to refresh.</param>
    /// <param name="reportItems">Answers Items labelled and ordered by the report.</param>
    /// <param name="dispose">Releases the server's report state.</param>
    public static FetchingPivotReportSource Fetch(IReadOnlyList<PivotField> fields, PivotSourceFeatures features,
        PivotReportUpdateMode updateMode,
        Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> window,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? items = null,
        Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? copy = null,
        Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? summary = null,
        Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>>? details = null,
        Func<CancellationToken, ValueTask>? refresh = null,
        Func<PivotReportItemsQuery, CancellationToken, ValueTask<PivotReportItemsResult>>? reportItems = null,
        Func<ValueTask>? dispose = null)
        => new(fields, features, updateMode, window, items, copy, summary, details, refresh, reportItems, dispose);
}

/// <summary>
/// A transport-neutral report source; delegates carry the complete versioned questions. It checks
/// each answer against its question — another request's reply, another Report Version, records of
/// another Source Version are refused or rejected, never shown. Window Changes are checked by the
/// client that applies them (<see cref="PivotReportClient"/>), against the digest of the Window they
/// make: whatever relays the server's Window Changes must pass them on whole, or build them
/// complete — every row of the Window whose shown values changed, totals and percentages included —
/// with the digest of the whole resulting Window (<see cref="PivotReportUpdate"/>).
/// </summary>
public sealed class FetchingPivotReportSource : PivotReportSource
{
    private readonly Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> _window;
    private readonly Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? _items;
    private readonly Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? _copy;
    private readonly Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? _summary;
    private readonly Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>>? _details;
    private readonly Func<CancellationToken, ValueTask>? _refresh;
    private readonly Func<PivotReportItemsQuery, CancellationToken, ValueTask<PivotReportItemsResult>>? _reportItems;
    private readonly Dictionary<PivotReportVersion, string> _reportedVersions = [];
    private readonly Func<ValueTask>? _dispose;
    private int _disposed;
    internal FetchingPivotReportSource(IReadOnlyList<PivotField> fields, PivotSourceFeatures features,
        PivotReportUpdateMode updateMode, Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> window,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? items,
        Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? copy,
        Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? summary,
        Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>>? details,
        Func<CancellationToken, ValueTask>? refresh,
        Func<PivotReportItemsQuery, CancellationToken, ValueTask<PivotReportItemsResult>>? reportItems, Func<ValueTask>? dispose)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Features = features;
        UpdateMode = updateMode;
        _window = window;
        _items = items;
        _copy = copy;
        _summary = summary;
        _details = details;
        _refresh = refresh;
        _reportItems = reportItems;
        _dispose = dispose;
    }
    /// <inheritdoc />
    public override IReadOnlyList<PivotField> Fields { get; }
    /// <inheritdoc />
    public override PivotSourceFeatures Features { get; }
    /// <inheritdoc />
    public override PivotReportUpdateMode UpdateMode { get; }
    /// <summary>Informs the report Consumer of a server notification.</summary>
    public void NotifyChanged(string? sourceVersion = null) => OnChanged(new(sourceVersion));
    /// <inheritdoc />
    public override async ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default)
    {
        var update = await _window(request, cancellationToken).ConfigureAwait(false);
        if (update?.Metadata is { } metadata && update.RequestId == request.RequestId && update.Window == request.Window)
        {
            lock (_reportedVersions)
            {
                _reportedVersions[metadata.Version] = metadata.SourceVersion;
                while (_reportedVersions.Count > 16) _reportedVersions.Remove(_reportedVersions.Keys.First());
            }
        }
        return update!;
    }
    /// <inheritdoc />
    public override async ValueTask<PivotItemPage> RawItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        var page = await (_items ?? throw Missing("Items"))(query, cancellationToken).ConfigureAwait(false);
        if (page is null || !page.IsRefused && (page.SourceVersion != query.SourceVersion || page.Items.Count > query.Max))
            throw new InvalidOperationException("The Items response does not answer the requested Source Version and extent.");
        return page;
    }
    /// <inheritdoc />
    public override async ValueTask<PivotReportItemsResult> ItemsAsync(PivotReportItemsQuery query, CancellationToken cancellationToken = default)
    {
        var result = await (_reportItems ?? throw Missing("report Items"))(query, cancellationToken).ConfigureAwait(false);
        if (result is null || result.Version != query.Version)
            return new(query.Version, "", [], 0, WrongVersion());
        if (result.Refusal is not null) return result;
        string? sourceVersion;
        lock (_reportedVersions) _reportedVersions.TryGetValue(query.Version, out sourceVersion);
        if (result.Items.Count > query.Max || result.Total < result.Items.Count
            || sourceVersion is not null && result.SourceVersion != sourceVersion
            || result.Items.Select(item => item.Key).Distinct().Count() != result.Items.Count)
            return new(query.Version, "", [], 0, Invalid("Items returned another version or an invalid extent."));
        return result;
    }
    /// <inheritdoc />
    public override async ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default)
    {
        var result = await (_copy ?? throw Missing("Copy"))(query, cancellationToken).ConfigureAwait(false);
        if (result is null || result.Version != query.Version)
            return new(query.Version, [], WrongVersion());
        if (result.Refusal is not null) return result;
        if (result.Blocks.Count != query.Ranges.Count)
            return new(query.Version, [], Invalid("Copy returned a different number of selected rectangles."));
        for (var i = 0; i < result.Blocks.Count; i++)
        {
            var range = query.Ranges[i];
            var block = result.Blocks[i];
            if (block.Rows.Count != (long)range.Bottom - range.Top + 1
                || block.Headers.Count != (long)range.Right - range.Left + 1
                || block.Rows.Any(row => row.Count != block.Headers.Count))
                return new(query.Version, [], Invalid("Copy returned a partial selected rectangle."));
        }
        return result;
    }
    /// <inheritdoc />
    public override async ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var result = await (_summary ?? throw Missing("Summary"))(query, cancellationToken).ConfigureAwait(false);
        return result is not null && result.Version == query.Version ? result
            : new(query.Version, default, default, default, false, null, "", WrongVersion());
    }
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The page answers another Source Version, another
    /// range or other fields than the question asked: records that would not add up to the cell are
    /// never shown.</exception>
    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await (_details ?? throw Missing("Details"))(query, cancellationToken).ConfigureAwait(false);
        if (page is null)
            throw new InvalidOperationException("Details returned no page.");
        if (page.IsRefused)
            return page;
        if (page.SourceVersion != query.SourceVersion || page.Start != query.Start || page.Records.Count > query.Count
            || !page.Fields.Select(field => field.Info).SequenceEqual(Fields.Select(field => field.Info)))
            throw new InvalidOperationException("Details answered another Source Version, range or fields than the question asked.");
        return page;
    }
    /// <inheritdoc />
    public override async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Features.CanRefresh) return;
        if (_refresh is { } refresh) await refresh(cancellationToken).ConfigureAwait(false);
        OnChanged(new());
    }
    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        lock (_reportedVersions) _reportedVersions.Clear();
        if (_dispose is { } dispose) await dispose().ConfigureAwait(false);
    }
    private static PivotReportRefusal WrongVersion() => Invalid("The response names another Report Version.");
    private static PivotReportRefusal Invalid(string message) => new(PivotReportRefusalKind.InvalidResponse, message);
    private static InvalidOperationException Missing(string operation) => new($"The report source has no {operation} transport.");
}
