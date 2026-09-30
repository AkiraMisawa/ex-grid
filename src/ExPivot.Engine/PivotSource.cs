namespace ExPivot.Engine;

/// <summary>
/// What ExPivot asks for a report's aggregates, a field's Items and the records behind a cell
/// (ADR-0065). ExPivot takes the shape ExGrid takes: it never opens a connection and holds no
/// data. The Consumer hands it a source:
/// <list type="bullet">
/// <item><see cref="From{TRecord}"/> answers from records in the process — in the browser under
/// WebAssembly, or in the host's process under Blazor Server — and is the reference
/// implementation: its answers are the engine's rules (ADR-0059);</item>
/// <item><see cref="Fetch"/> carries the Consumer's transport to a server, which answers with the
/// Leaf Aggregates and is held to the reference's answers, question for question.</item>
/// </list>
/// Every question and every answer is a serialisable value (<see cref="PivotJson"/>). Every
/// answer carries its Source Version, and a field's Items and a cell's records are asked for
/// under it, so the records behind a cell always add up to it.
/// </summary>
public abstract class PivotSource
{
    /// <summary>Creates a source.</summary>
    protected PivotSource()
    {
    }

    /// <summary>The fields the source offers — what the Field List shows.</summary>
    public abstract IReadOnlyList<PivotField> Fields { get; }

    /// <summary>The Aggregations the source answers, and whether it can be refreshed.</summary>
    public abstract PivotSourceFeatures Features { get; }

    /// <summary>
    /// Answers a question with the Leaf Aggregates, or refuses it (ADR-0065): more leaves than
    /// <see cref="PivotQuery.MaxLeaves"/>, a field it does not have, or a part only an Aggregation
    /// it does not offer reads. A cancelled question throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public abstract ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default);

    /// <summary>A field's Items over all the data, under the Source Version asked; or a refusal.</summary>
    public abstract ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default);

    /// <summary>One page of the records behind a cell, under the Source Version asked; or a refusal.</summary>
    public abstract ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default);

    /// <summary>Excel's Refresh: a source that can be refreshed (<see cref="PivotSourceFeatures.CanRefresh"/>)
    /// raises <see cref="Changed"/>, and ExPivot asks again. One that cannot does nothing.</summary>
    public abstract ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when the source's data moved on: ExPivot asks again for the whole answer
    /// (ADR-0066). Raised on whatever thread the change was learned on; a component marshals it to
    /// its own.</summary>
    public event Action<PivotSourceChanged>? Changed;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    protected void OnChanged(PivotSourceChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Changed?.Invoke(change);
    }

    /// <summary>
    /// The bundled source over records in memory — the reference implementation (ADR-0065): it
    /// answers every question by the engine's rules (ADR-0059), reading each record through the
    /// fields' accessors. The records are its data at one Source Version, fixed for the instance:
    /// a new list is a new source, which is Excel's Refresh, and a question asked under another
    /// version is refused. It works in slices and yields between them (<paramref name="slicing"/>),
    /// and a cancelled question stops at the next slice.
    /// </summary>
    /// <param name="records">The Source Records, read and never written. The list must not change
    /// while the source is in use: a changed list is a new source.</param>
    /// <param name="fields">The Pivot Fields, each with its accessor; names unique.</param>
    /// <param name="slicing">How it shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    public static PivotSource From<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotSlicing? slicing = null)
        => new RecordPivotSource<TRecord>(records, fields, slicing ?? PivotSlicing.Default);

    /// <summary>
    /// A source a server answers, through the Consumer's transport (ADR-0065), as
    /// <c>GridSource.Fetch</c> is ExGrid's (ADR-0025). Authentication, retries and the transport
    /// are the Consumer's; ExPivot ships no HTTP client and no endpoint. Each delegate typically
    /// writes its question with <see cref="PivotJson"/>, sends it, and reads the answer back.
    /// A question naming a field the source does not declare, or asking for a part none of its
    /// offered Aggregations reads, is refused here without asking; an answer to another question
    /// than the one asked is refused by name.
    /// </summary>
    /// <param name="fields">The fields the server offers.</param>
    /// <param name="features">The Aggregations it answers, and whether it can be refreshed.</param>
    /// <param name="aggregate">Answers a <see cref="PivotQuery"/>.</param>
    /// <param name="items">Answers a <see cref="PivotItemsQuery"/>.</param>
    /// <param name="details">Answers a <see cref="PivotDetailsQuery"/>.</param>
    public static FetchingPivotSource Fetch(
        IReadOnlyList<PivotField> fields,
        PivotSourceFeatures features,
        Func<PivotQuery, CancellationToken, ValueTask<PivotAnswer>> aggregate,
        Func<PivotItemsQuery, CancellationToken, ValueTask<PivotItemPage>> items,
        Func<PivotDetailsQuery, CancellationToken, ValueTask<PivotDetailPage>> details)
        => new(fields, features, aggregate, items, details);

    // ---- What every source refuses the same way ------------------------------------------------

    /// <summary>The fields by name, refusing a null field and a name declared twice — a layout
    /// could not say which it means.</summary>
    internal static Dictionary<string, TField> Declared<TField>(IReadOnlyList<TField> fields)
        where TField : PivotField
    {
        ArgumentNullException.ThrowIfNull(fields);
        var declared = new Dictionary<string, TField>(fields.Count, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field, nameof(fields));
            if (!declared.TryAdd(field.Name, field))
                throw new ArgumentException($"Two Pivot Fields are named '{field.Name}'; a layout could not say which it means.", nameof(fields));
        }
        return declared;
    }

    private bool Knows(string field)
    {
        foreach (var declared in Fields)
        {
            if (declared.Name == field)
                return true;
        }
        return false;
    }

    /// <summary>Why this source refuses <paramref name="query"/> before reading anything, or null:
    /// a field it does not have, or a part none of its offered Aggregations reads.</summary>
    internal PivotSourceRefusal? Refusal(PivotQuery query)
    {
        foreach (var field in query.Placed)
        {
            if (!Knows(field.Field))
                return PivotSourceRefusal.UnknownField(field.Field);
        }
        foreach (var value in query.Values)
        {
            if (!Knows(value.Field))
                return PivotSourceRefusal.UnknownField(value.Field);
            foreach (var part in new[] { PivotParts.Sum, PivotParts.Extremes, PivotParts.Product, PivotParts.Variance })
            {
                if ((value.Parts & part) != 0 && Features.MissingFor(part) is { Length: > 0 } missing)
                    return PivotSourceRefusal.AggregationNotOffered(value.Field, missing);
            }
        }
        return null;
    }

    /// <summary>Why this source refuses <paramref name="query"/> before reading anything, or null.</summary>
    internal PivotSourceRefusal? Refusal(PivotItemsQuery query)
        => Knows(query.Field) ? null : PivotSourceRefusal.UnknownField(query.Field);

    /// <summary>Why this source refuses <paramref name="query"/> before reading anything, or null.</summary>
    internal PivotSourceRefusal? Refusal(PivotDetailsQuery query)
    {
        foreach (var field in query.RowItems.Select(i => i.Field)
                     .Concat(query.ColumnItems.Select(i => i.Field))
                     .Concat(query.HiddenItems.Select(h => h.Field)))
        {
            if (!Knows(field))
                return PivotSourceRefusal.UnknownField(field);
        }
        return null;
    }
}
