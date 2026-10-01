using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// What ExPivot asks for a report's aggregates, a field's Items and the records behind a cell
/// (ADR-0065). ExPivot takes the shape ExGrid takes: it never opens a connection and holds no
/// data. The Consumer hands it a source:
/// <list type="bullet">
/// <item><see cref="From(Snapshot, IReadOnlyList{PivotField}?, PivotSlicing?)"/> answers from a
/// Snapshot in the process — in the browser under WebAssembly, or in the host's process under
/// Blazor Server — and is the reference implementation: its answers are the engine's rules
/// (ADR-0059). Records in memory come to it through typed field declarations
/// (<see cref="PivotFields.Of{T}"/>), or through untyped accessors;</item>
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
    /// The bundled source over a Snapshot — the reference implementation (ADR-0065): it answers
    /// every question from the Snapshot's columns by the engine's rules (ADR-0059), in slices,
    /// yielding between them (<paramref name="slicing"/>); a cancelled question stops at the next
    /// slice. It takes Change Batches (<see cref="SnapshotPivotSource.Apply"/>, ADR-0066).
    /// </summary>
    /// <param name="snapshot">The data.</param>
    /// <param name="fields">The Pivot Fields, each reading the column its
    /// <see cref="PivotField.Column"/> names, or the column of its own name — a date part reads the
    /// Date column it is a part of; or null for one field per column, captioned as the column is
    /// and declared by its kind: Text as Text, Decimal, Double and Integer as Number, Date as Date,
    /// Boolean as Boolean.</param>
    /// <param name="slicing">How it shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <exception cref="ArgumentException">A field names a column the Snapshot does not have, or is a
    /// date part of a column that is not a Date column; two fields share a name.</exception>
    public static SnapshotPivotSource From(Snapshot snapshot, IReadOnlyList<PivotField>? fields = null, PivotSlicing? slicing = null)
        => new(snapshot, fields, slicing ?? PivotSlicing.Default);

    /// <summary>
    /// The bundled source over records in memory, declared with typed fields (ADR-0063/0065, Q53) —
    /// the standard way: the records are read once, on the calling thread, into a Snapshot whose
    /// columns the declarations made, boxing no value, and every question is answered from it.
    /// <see cref="FromAsync"/> reads them in slices, which a browser needs for a large list.
    /// </summary>
    /// <param name="records">The Source Records, read once and kept, by reference, behind their rows.</param>
    /// <param name="fields">The typed declarations (<see cref="PivotFields.Of{T}"/>).</param>
    /// <param name="slicing">How it shares the thread while it answers; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or
    /// carried twice: nothing is built, and the row and the column are named (ADR-0063).</exception>
    public static SnapshotPivotSource From<TRecord>(IReadOnlyList<TRecord> records, PivotFields<TRecord> fields, PivotSlicing? slicing = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        return new(fields.Build(records), fields.Fields, slicing ?? PivotSlicing.Default);
    }

    /// <summary>
    /// <see cref="From{TRecord}(IReadOnlyList{TRecord}, PivotFields{TRecord}, PivotSlicing?)"/> with the
    /// records read in slices, yielding between them as the source does and reporting progress
    /// (ADR-0063): a browser keeps painting while a million records are read. A cancelled read
    /// builds nothing.
    /// </summary>
    /// <param name="records">The Source Records, read once and kept, by reference, behind their rows.</param>
    /// <param name="fields">The typed declarations (<see cref="PivotFields.Of{T}"/>).</param>
    /// <param name="slicing">How it shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="progress">Told how far the read has come after every slice.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or
    /// carried twice.</exception>
    public static async ValueTask<SnapshotPivotSource> FromAsync<TRecord>(
        IReadOnlyList<TRecord> records,
        PivotFields<TRecord> fields,
        PivotSlicing? slicing = null,
        IProgress<SnapshotProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        var shared = slicing ?? PivotSlicing.Default;
        var options = new SnapshotLoadOptions
        {
            SliceBudget = shared.Budget,
            Yield = () => shared.YieldAsync(cancellationToken),
            Progress = progress,
        };
        var snapshot = await fields.BuildAsync(records, options, cancellationToken).ConfigureAwait(false);
        return new(snapshot, fields.Fields, shared);
    }

    /// <summary>
    /// The bundled source over records in memory read through untyped accessors — a reference
    /// implementation like the others (ADR-0065). On the first question the records are read once,
    /// in slices, into a Snapshot, each value keeping its own kind — a field "whose values are not
    /// all of the declared type is still pivoted as its values are" (ADR-0059) — and every question
    /// is answered from it by the engine's rules. Typed declarations
    /// (<see cref="From{TRecord}(IReadOnlyList{TRecord}, PivotFields{TRecord}, PivotSlicing?)"/>) box nothing,
    /// and are the standard way.
    /// <para>The records are its data at one Source Version, fixed for the instance: a new list is
    /// a new source, which is Excel's Refresh, and a question asked under another version is
    /// refused. It works in slices and yields between them (<paramref name="slicing"/>), and a
    /// cancelled question stops at the next slice.</para>
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
