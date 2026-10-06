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
    public abstract ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default);
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
        Func<CancellationToken, ValueTask>? refresh = null)
        => new(fields, features, updateMode, window, items, copy, summary, details, refresh);
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
    internal FetchingPivotReportSource(IReadOnlyList<PivotField> fields, PivotSourceFeatures features,
        PivotReportUpdateMode updateMode, Func<PivotReportRequest, CancellationToken, ValueTask<PivotReportUpdate>> window,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>>? items,
        Func<PivotReportCopyQuery, CancellationToken, ValueTask<PivotReportCopyResult>>? copy,
        Func<PivotReportSummaryQuery, CancellationToken, ValueTask<PivotReportSummaryResult>>? summary,
        Func<PivotReportDetailsQuery, CancellationToken, ValueTask<PivotReportDetailsResult>>? details,
        Func<CancellationToken, ValueTask>? refresh)
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
    public override ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default)
        => _window(request, cancellationToken);
    /// <inheritdoc />
    public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
        => _items is { } read ? read(query, cancellationToken) : throw Missing("Items");
    /// <inheritdoc />
    public override ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default)
        => _copy is { } read ? read(query, cancellationToken) : throw Missing("Copy");
    /// <inheritdoc />
    public override ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default)
        => _summary is { } read ? read(query, cancellationToken) : throw Missing("Summary");
    /// <inheritdoc />
    public override ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken cancellationToken = default)
        => _details is { } read ? read(query, cancellationToken) : throw Missing("Details");
    /// <inheritdoc />
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => _refresh?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;
    /// <inheritdoc />
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private static InvalidOperationException Missing(string operation) => new($"The report source has no {operation} transport.");
}
