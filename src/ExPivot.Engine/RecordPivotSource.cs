using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// <c>PivotSource.From(records, fields)</c>: the bundled source over records in memory read through
/// untyped accessors, and the reference implementation (ADR-0065). The records are read once, on
/// the first question and in slices, into a Snapshot (<see cref="RecordColumns{TRecord}"/>), and
/// every question is answered from it by the engine's rules (ADR-0059), as
/// <see cref="SnapshotPivotSource"/> answers. The records behind a cell carry the values the
/// accessors read, and the records themselves.
///
/// <para>Its Source Version is fixed for the instance: the records are one state of the data, and
/// a new list is a new source. It works in slices (<see cref="PivotSlicing"/>).</para>
/// </summary>
internal sealed class RecordPivotSource<TRecord> : PivotSource
{
    private readonly IReadOnlyList<TRecord> _records;
    private readonly PivotField<TRecord>[] _fields;
    private readonly PivotSlicing _slicing;
    private readonly Lock _gate = new();
    private Task<(Snapshot Snapshot, Dictionary<string, FieldBinding> Bindings)>? _data;

    public RecordPivotSource(IReadOnlyList<TRecord> records, IReadOnlyList<PivotField<TRecord>> fields, PivotSlicing slicing)
    {
        ArgumentNullException.ThrowIfNull(records);
        Declared(fields);
        _fields = [.. fields];
        _records = records;
        _slicing = slicing ?? throw new ArgumentNullException(nameof(slicing));
        SourceVersion = Guid.NewGuid().ToString("N");
    }

    /// <summary>The one state of the data this source answers under.</summary>
    public string SourceVersion { get; }

    public override IReadOnlyList<PivotField> Fields => _fields;

    public override PivotSourceFeatures Features => PivotSourceFeatures.All;

    public override async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var (snapshot, bindings) = await DataAsync(cancellationToken);
        var pass = new AggregationPass(snapshot, bindings, query, keepRows: false, _slicing.RowsRead);
        await SlicedRun.RunAsync(pass.RowCount, pass.Step, _slicing, cancellationToken);
        pass.Complete();
        return pass.Answer(SourceVersion);
    }

    /// <summary>The same answer, in one pass on the calling thread — for the engine's own
    /// synchronous entry points.</summary>
    public PivotAnswer Aggregate(PivotQuery query)
    {
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var (snapshot, bindings) = Data();
        var pass = new AggregationPass(snapshot, bindings, query, keepRows: false, null);
        pass.Step(0, pass.RowCount);
        pass.Complete();
        return pass.Answer(SourceVersion);
    }

    public override async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotItemPage.Refused(refusal);
        if (query.SourceVersion != SourceVersion)
            return PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var (snapshot, bindings) = await DataAsync(cancellationToken);
        var pass = new ItemsPass(snapshot, bindings[query.Field], _slicing.RowsRead);
        await SlicedRun.RunAsync(pass.RowCount, pass.Step, _slicing, cancellationToken);
        return pass.Page(query, SourceVersion);
    }

    /// <summary>Every Item of a field over all the records, first spellings kept — for the
    /// engine's own synchronous entry points.</summary>
    public IReadOnlyList<ItemKey> AllItems(string field)
    {
        var (snapshot, bindings) = Data();
        var pass = new ItemsPass(snapshot, bindings[field], null);
        pass.Step(0, pass.RowCount);
        return pass.Keys();
    }

    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        if (query.SourceVersion != SourceVersion)
            return PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var (snapshot, bindings) = await DataAsync(cancellationToken);
        var pass = new DetailsPass(snapshot, bindings, query, _slicing.RowsRead);
        await SlicedRun.RunAsync(pass.RowCount, pass.Step, _slicing, cancellationToken);
        var records = new PivotDetailRecord[pass.Matches.Count];
        var values = new object?[_fields.Length];
        for (var i = 0; i < records.Length; i++)
        {
            // The records are the Snapshot's rows, in order: a row's position is its record's index.
            var record = _records[pass.Matches[i].Position];
            for (var f = 0; f < _fields.Length; f++)
                values[f] = _fields[f].Value(record);
            records[i] = new PivotDetailRecord(values, record);
        }
        return new PivotDetailPage(SourceVersion, _fields, query.Start, pass.Total, records);
    }

    /// <summary>The records behind a cell, all of them, on the calling thread, whatever the
    /// question's version — for the engine's own synchronous entry points, which hand it the
    /// records they mean.</summary>
    public IReadOnlyList<TRecord> RecordsBehind(PivotDetailsQuery query)
    {
        if (Refusal(query) is { } refusal)
            throw new InvalidOperationException(refusal.Message);
        var (snapshot, bindings) = Data();
        var pass = new DetailsPass(snapshot, bindings, query, null);
        pass.Step(0, pass.RowCount);
        return [.. pass.Matches.Select(match => _records[match.Position])];
    }

    /// <summary>Nothing to refresh: the records are one state of the data, and a new list is a
    /// new source (ADR-0065).</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    // The Snapshot, read in slices on the first question. A question cancelled while it is read
    // stops waiting for it, and the read carries on for the next question; a read that failed —
    // an accessor that threw — fails every question, naming the row and the field.
    private async ValueTask<(Snapshot, Dictionary<string, FieldBinding>)> DataAsync(CancellationToken cancellationToken)
    {
        Task<(Snapshot Snapshot, Dictionary<string, FieldBinding> Bindings)> data;
        lock (_gate)
            data = _data ??= RecordColumns<TRecord>.BuildAsync(_records, _fields, _slicing);
        return await data.WaitAsync(cancellationToken);
    }

    private (Snapshot, Dictionary<string, FieldBinding>) Data()
    {
        lock (_gate)
        {
            if (_data is { IsCompletedSuccessfully: true } done)
                return done.Result;
            var built = RecordColumns<TRecord>.Build(_records, _fields);
            _data = Task.FromResult(built);
            return built;
        }
    }
}
