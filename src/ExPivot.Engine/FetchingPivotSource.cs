namespace ExPivot.Engine;

/// <summary>
/// <c>PivotSource.Fetch</c>: a Pivot Source a server answers, through three delegates carrying the
/// Consumer's transport (ADR-0066). ExPivot never opens a connection; authentication, retries and
/// the transport itself are the Consumer's.
///
/// <para>The Consumer tells it when the server's data moved on — through SignalR, polling or a
/// message bus — with <see cref="NotifyChanged"/>, and ExPivot asks again for the whole answer
/// (ADR-0067). Refresh does the same.</para>
/// </summary>
public sealed class FetchingPivotSource : PivotSource
{
    private readonly PivotField[] _fields;
    private readonly Func<PivotQuery, CancellationToken, ValueTask<PivotAnswer>> _aggregate;
    private readonly Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>> _items;
    private readonly Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>> _details;

    internal FetchingPivotSource(
        IReadOnlyList<PivotField> fields,
        PivotSourceFeatures features,
        Func<PivotQuery, CancellationToken, ValueTask<PivotAnswer>> aggregate,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>> items,
        Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>> details)
    {
        Declared(fields);
        _fields = [.. fields];
        Features = features ?? throw new ArgumentNullException(nameof(features));
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _details = details ?? throw new ArgumentNullException(nameof(details));
    }

    /// <inheritdoc />
    public override IReadOnlyList<PivotField> Fields => _fields;

    /// <inheritdoc />
    public override PivotSourceFeatures Features { get; }

    /// <summary>Asks the server, unless the question names a field the source does not declare or
    /// asks for a part none of its offered Aggregations reads — refused here, without asking. An
    /// answer to another question is refused by name.</summary>
    public override async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var answer = await _aggregate(query, cancellationToken) ?? throw new InvalidOperationException("The source's aggregate delegate answered null.");
        if (!answer.IsRefused && answer.Mismatch(query) is { } mismatch)
            throw new InvalidOperationException($"The server answered another question than the one asked: {mismatch} (ADR-0066).");
        return answer;
    }

    /// <summary>Asks the server, unless the field is not the source's.</summary>
    public override async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotItemPage.Refused(refusal);
        var page = await _items(query, cancellationToken) ?? throw new InvalidOperationException("The source's items delegate answered null.");
        if (!page.IsRefused && page.Items.Count > query.Max)
            throw new InvalidOperationException($"The server answered {page.Items.Count} Items, and {query.Max} were asked for (ADR-0066).");
        return page;
    }

    /// <summary>Asks the server, unless a field of the cell is not the source's. A page whose
    /// fields are not the source's, or that holds more records than asked, is refused by name.</summary>
    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        var page = await _details(query, cancellationToken) ?? throw new InvalidOperationException("The source's details delegate answered null.");
        if (page.IsRefused)
            return page;
        if (!page.Fields.Select(f => (f.Name, f.Type)).SequenceEqual(_fields.Select(f => (f.Name, f.Type))))
            throw new InvalidOperationException($"The server's records carry the fields [{string.Join(", ", page.Fields.Select(f => f.Name))}], and the source declares [{string.Join(", ", _fields.Select(f => f.Name))}] (ADR-0066).");
        if (page.Records.Count > query.Count || page.Start != query.Start)
            throw new InvalidOperationException($"The server answered {page.Records.Count} records from {page.Start}, and {query.Count} from {query.Start} were asked for (ADR-0066).");
        return page;
    }

    /// <summary>Excel's Refresh: raises <see cref="PivotSource.Changed"/>, and ExPivot asks again
    /// for the whole answer (ADR-0066).</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnChanged(new PivotSourceChanged());
        return ValueTask.CompletedTask;
    }

    /// <summary>Tells the source that the server's data moved on (ADR-0067), however the Consumer
    /// learned it: <see cref="PivotSource.Changed"/> is raised, and ExPivot asks again for the
    /// whole answer.</summary>
    /// <param name="sourceVersion">The data's new Source Version, when the Consumer knows it.</param>
    public void NotifyChanged(string? sourceVersion = null) => OnChanged(new PivotSourceChanged(sourceVersion));
}
