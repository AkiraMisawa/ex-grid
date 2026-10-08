namespace ExPivot.Engine;

/// <summary>The asynchronous report boundary shared by a local browser and a remote server (ADR-0151).</summary>
public abstract class PivotReportSource : IAsyncDisposable
{
    /// <summary>Fields offered to the Field List.</summary>
    public abstract IReadOnlyList<PivotField> Fields { get; }
    /// <summary>Offered Aggregations and Refresh capability.</summary>
    public abstract PivotSourceFeatures Features { get; }
    /// <summary>Whether changed contributions can be calculated incrementally.</summary>
    public abstract PivotReportUpdateMode UpdateMode { get; }
    /// <summary>A complete report Window or a delta from its named baseline.</summary>
    public abstract ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default);
    /// <summary>A field's Items at the requested Source Version.</summary>
    public abstract ValueTask<PivotItemPage> RawItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default);
    /// <summary>Items labeled and ordered in the report computation process.</summary>
    public abstract ValueTask<PivotReportItemsResult> ItemsAsync(PivotReportItemsQuery query, CancellationToken cancellationToken = default);
    /// <summary>All selected cells at the requested Report Version, including offscreen cells.</summary>
    public abstract ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default);
    /// <summary>The selected cells' summary at the requested Report Version.</summary>
    public abstract ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default);
    /// <summary>The source records contributing to the requested version's cell.</summary>
    public abstract ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken cancellationToken = default);
    /// <summary>Asks the provider to refresh.</summary>
    public abstract ValueTask RefreshAsync(CancellationToken cancellationToken = default);
    /// <summary>Releases this report's calculation state; it does not own the underlying data provider.</summary>
    public abstract ValueTask DisposeAsync();
    /// <summary>The provider learned that its data changed.</summary>
    public event Action<PivotSourceChanged>? Changed;
    /// <summary>Raises the data-change notification.</summary>
    protected void OnChanged(PivotSourceChanged change) => Changed?.Invoke(change);

    /// <summary>Runs a report over a local data provider, or on a server behind the Consumer's transport.</summary>
    /// <param name="source">The data provider; this report does not own its lifetime.</param>
    /// <param name="orderKeys">Server-registered Order Key functions, by policy identifier.</param>
    /// <param name="versionsKept">Bounded number of immutable reports available to versioned operations.</param>
    /// <param name="slicing">How calculation work shares the calling thread.</param>
    /// <param name="timeProvider">The clock that timestamps shown-text changes.</param>
    public static LocalPivotReportSource From(PivotSource source,
        IReadOnlyDictionary<string, Func<object, IComparable?>>? orderKeys = null,
        int versionsKept = 2, TimeProvider? timeProvider = null, PivotSlicing? slicing = null)
        => new(source, orderKeys, versionsKept, timeProvider ?? TimeProvider.System, slicing ?? PivotSlicing.Default);

    /// <summary>Uses the Consumer's transport. Authentication, connection and server report lifetime remain the Consumer's.</summary>
    public static FetchingPivotReportSource Fetch(IReadOnlyList<PivotField> fields, PivotSourceFeatures features,
        PivotReportUpdateMode updateMode,
        Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> window,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? items = null,
        Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? copy = null,
        Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? summary = null,
        Func<PivotReportDetailsQuery, CancellationToken, ValueTask<PivotReportDetailsResult>>? details = null,
        Func<CancellationToken, ValueTask>? refresh = null,
        Func<PivotReportItemsQuery, CancellationToken, ValueTask<PivotReportItemsResult>>? reportItems = null,
        Func<ValueTask>? dispose = null)
        => new(fields, features, updateMode, window, items, copy, summary, details, refresh, reportItems, dispose);
}

/// <summary>A transport-neutral report source; delegates carry the complete versioned questions.</summary>
public sealed class FetchingPivotReportSource : PivotReportSource
{
    private readonly Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> _window;
    private readonly Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? _items;
    private readonly Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? _copy;
    private readonly Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? _summary;
    private readonly Func<PivotReportDetailsQuery, CancellationToken, ValueTask<PivotReportDetailsResult>>? _details;
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
        Func<PivotReportDetailsQuery, CancellationToken, ValueTask<PivotReportDetailsResult>>? details,
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
    public override async ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken cancellationToken = default)
    {
        var result = await (_details ?? throw Missing("Details"))(query, cancellationToken).ConfigureAwait(false);
        if (result is null || result.Version != query.Version) return new(query.Version, null, WrongVersion());
        if (result.Refusal is not null) return result;
        if (result.Page is not { } page) return new(query.Version, null, Invalid("Details returned no page."));
        if (page.IsRefused) return result;
        string? sourceVersion;
        lock (_reportedVersions) _reportedVersions.TryGetValue(query.Version, out sourceVersion);
        if (sourceVersion is not null && page.SourceVersion != sourceVersion || page.Start != query.Start || page.Records.Count > query.Count
            || !page.Fields.Select(field => field.Info).SequenceEqual(Fields.Select(field => field.Info)))
            return new(query.Version, null, Invalid("Details returned another report's source records or a different extent."));
        return result;
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
