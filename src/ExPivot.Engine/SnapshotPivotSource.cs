using System.Globalization;
using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// The bundled Pivot Source over a Snapshot (<see cref="PivotSource.From(Snapshot, IReadOnlyList{PivotField}?, PivotSlicing?)"/>),
/// and the reference implementation (ADR-0065): it answers every question from the Snapshot's
/// columns by the engine's rules (ADR-0059), in slices, and a server that holds a Snapshot answers
/// with this same source.
/// <para>
/// <b>Live data</b> (ADR-0066): <see cref="Apply"/> takes a Change Batch. The Snapshot makes the
/// next one, the answer the source holds for its current question is brought up to date from what
/// the batch removed and added rather than read again — the exact parts by subtraction and
/// addition, every other part recomputed for the leaves the batch touched — and
/// <see cref="PivotSource.Changed"/> is raised with the new Source Version. Asked the same question
/// again, the source answers from what it holds; after any sequence of batches, every leaf equals a
/// fresh aggregation of the Snapshot they made, to the last bit. A batch that compacted the
/// Snapshot moved its rows, and the next question is read afresh. An answer is assembled from what
/// the source holds in slices (PV-40), and a batch applied meanwhile waits until it is made, so
/// that no answer is half a batch.
/// </para>
/// <para>
/// <b>Source Version</b>: this source's own name and the Snapshot's version, so a new source is a
/// new version (ADR-0065). It answers a field's Items and a cell's records under its current
/// version and under the versions of its last <see cref="AnswersHeld"/> answers — a Snapshot is
/// immutable, so their records still add up — and refuses an older one
/// (<see cref="PivotSourceRefusalKind.SourceVersionNotHeld"/>): holding every version would hold
/// every Snapshot a live feed ever made.
/// </para>
/// </summary>
public sealed class SnapshotPivotSource : PivotSource
{
    /// <summary>How many of its latest answers' versions the source keeps answering Items and
    /// records under, besides its current one.</summary>
    public const int AnswersHeld = 4;

    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly PivotField[] _fields;
    private readonly Dictionary<string, FieldBinding> _bindings;
    private readonly PivotSlicing _slicing;
    private readonly Lock _gate = new();
    private readonly List<Snapshot> _answered = [];
    private Snapshot _snapshot;
    private AggregationPass? _held;
    private PivotAnswer? _heldAnswer;

    internal SnapshotPivotSource(Snapshot snapshot, IReadOnlyList<PivotField>? fields, PivotSlicing slicing)
    {
        (_fields, _bindings) = FieldBinding.Of(snapshot, fields);
        _snapshot = snapshot;
        _slicing = slicing ?? throw new ArgumentNullException(nameof(slicing));
    }

    /// <summary>The Snapshot the source answers from now: the one it was made with, or the one the
    /// last Change Batch made.</summary>
    public Snapshot Snapshot
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    /// <summary>The pass held for the current question, for layer 1: what a batch folds into.</summary>
    internal AggregationPass? HeldPass
    {
        get
        {
            lock (_gate)
                return _held;
        }
    }

    /// <summary>The Source Version the source answers under now.</summary>
    public string SourceVersion
    {
        get
        {
            lock (_gate)
                return VersionOf(_snapshot);
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<PivotField> Fields => _fields;

    /// <summary>Every Aggregation, and no Refresh: the bundled source is refreshed by a Change Batch
    /// or by a new source (ADR-0065).</summary>
    public override PivotSourceFeatures Features => PivotSourceFeatures.All;

    /// <summary>
    /// Answers with the Leaf Aggregates of the current Snapshot (ADR-0065), read in slices — or, for
    /// the question it answered last, from the answer it holds and has folded every batch into
    /// since. Refuses, as soon as it is passed, an answer of more leaves than
    /// <see cref="PivotQuery.MaxLeaves"/>. The answer is assembled in slices too, after the last
    /// slice of rows (PV-40), and a cancelled question throws at the next slice. A batch applied
    /// while the answer held is being assembled is folded in once it is made: no answer is half a
    /// batch (ADR-0066).
    /// </summary>
    public override async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        Snapshot snapshot;
        AggregationPass? assembling = null;
        lock (_gate)
        {
            snapshot = _snapshot;
            if (!HeldUpToDate())
                _held = null;
            if (_held is { } held && held.Query.Equals(query) && ReferenceEquals(held.Snapshot, snapshot))
            {
                Remember(snapshot);
                if (_heldAnswer is { } answered)
                    return answered;
                held.Assembling++;
                assembling = held;
            }
        }
        var slicer = Slicer.Of(_slicing, cancellationToken);
        if (assembling is not null)
            return await AssembleHeldAsync(assembling, snapshot, slicer).ConfigureAwait(false);
        var pass = new AggregationPass(snapshot, _bindings, query, keepRows: snapshot.RecordKey is not null, _slicing.RowsRead);
        await slicer.PassAsync(pass.RowCount, pass.Step).ConfigureAwait(false);
        await pass.CompleteAsync(slicer).ConfigureAwait(false);
        var answer = await pass.AnswerAsync(VersionOf(snapshot), slicer).ConfigureAwait(false);
        lock (_gate)
        {
            if (pass.Refusal is null && ReferenceEquals(_snapshot, snapshot))
            {
                _held = pass;
                _heldAnswer = answer;
            }
            if (pass.Refusal is null)
                Remember(snapshot);
        }
        return answer;
    }

    // An answer assembled from the pass held for the current question, in slices (PV-40). Nothing
    // changes the pass meanwhile: a batch applied now waits (Apply), and is folded in once no
    // assembly reads the pass (ADR-0066).
    private async ValueTask<PivotAnswer> AssembleHeldAsync(AggregationPass held, Snapshot snapshot, Slicer slicer)
    {
        PivotAnswer? answer = null;
        try
        {
            answer = await held.AnswerAsync(VersionOf(snapshot), slicer).ConfigureAwait(false);
            return answer;
        }
        finally
        {
            lock (_gate)
            {
                held.Assembling--;
                // Kept for the next asking, unless the source has moved on meanwhile.
                if (answer is not null && ReferenceEquals(_held, held) && ReferenceEquals(_snapshot, snapshot))
                    _heldAnswer = answer;
                if (ReferenceEquals(_held, held) && !HeldUpToDate())
                    _held = null;
            }
        }
    }

    // Under the lock: folds in the batches the held pass deferred, once no answer is being
    // assembled from it. False when one could not be folded, and the pass is to be dropped.
    private bool HeldUpToDate() => _held is not { } held || held.Assembling > 0 || held.FoldDeferred();

    /// <summary>A field's Items over all the data of the version asked, in slices; refused under a
    /// version the source no longer holds.</summary>
    public override async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotItemPage.Refused(refusal);
        if (Held(query.SourceVersion) is not { } snapshot)
            return PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var pass = new ItemsPass(snapshot, _bindings[query.Field], _slicing.RowsRead);
        await Slicer.Of(_slicing, cancellationToken).PassAsync(pass.RowCount, pass.Step).ConfigureAwait(false);
        return pass.Page(query, query.SourceVersion);
    }

    /// <summary>A page of the records behind a cell in the version asked, in the Snapshot's order,
    /// read in slices; refused under a version the source no longer holds. A record carries the
    /// Consumer's object when the Snapshot keeps them.</summary>
    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refusal(query) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        if (Held(query.SourceVersion) is not { } snapshot)
            return PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var pass = new DetailsPass(snapshot, _bindings, query, _slicing.RowsRead);
        await Slicer.Of(_slicing, cancellationToken).PassAsync(pass.RowCount, pass.Step).ConfigureAwait(false);
        var records = new PivotDetailRecord[pass.Matches.Count];
        var values = new object?[_fields.Length];
        for (var i = 0; i < records.Length; i++)
        {
            var row = pass.Matches[i].Row;
            var slice = snapshot.Slice(row.Slice);
            for (var f = 0; f < _fields.Length; f++)
                values[f] = RowValues.DetailAt(_bindings[_fields[f].Name], snapshot, slice, row.Offset);
            records[i] = new PivotDetailRecord(values, snapshot.RecordAt(row));
        }
        return new PivotDetailPage(query.SourceVersion, _fields, query.Start, pass.Total, records);
    }

    /// <summary>Nothing to refresh: the bundled source shows no Refresh, and is moved on by a Change
    /// Batch (<see cref="Apply"/>) or replaced by a new source (ADR-0065).</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Applies a Change Batch (ADR-0066): the Snapshot makes the next one, whole or not at all; the
    /// answer held for the current question is brought up to date from what the batch removed and
    /// added; and <see cref="PivotSource.Changed"/> is raised with the new Source Version, after the
    /// source has moved on.
    /// </summary>
    /// <returns>What the Snapshot made: the next Snapshot, and what it removed and added.</returns>
    /// <exception cref="SnapshotException">The batch is refused, naming the key; nothing of it is
    /// applied and the source stays where it was.</exception>
    public SnapshotChange Apply(ChangeBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        SnapshotChange change;
        string version;
        lock (_gate)
        {
            change = _snapshot.Apply(batch);
            _snapshot = change.After;
            if (_held is { } held)
            {
                // An answer being assembled from the pass reads it in slices: the batch waits until
                // it is made, so that the answer is the version before the batch, whole (ADR-0066).
                if (held.Assembling > 0)
                    held.Defer(change);
                else if (!held.FoldDeferred() || !held.Fold(change))
                    _held = null;
            }
            _heldAnswer = null;
            version = VersionOf(_snapshot);
        }
        OnChanged(new PivotSourceChanged(version));
        return change;
    }

    private string VersionOf(Snapshot snapshot) => _id + ":" + snapshot.Version.ToString(CultureInfo.InvariantCulture);

    // The Snapshot of a version the source still answers under: the current one, or one of its last
    // answers'.
    private Snapshot? Held(string version)
    {
        lock (_gate)
        {
            if (VersionOf(_snapshot) == version)
                return _snapshot;
            foreach (var snapshot in _answered)
            {
                if (VersionOf(snapshot) == version)
                    return snapshot;
            }
            return null;
        }
    }

    private void Remember(Snapshot snapshot)
    {
        _answered.Remove(snapshot);
        _answered.Add(snapshot);
        if (_answered.Count > AnswersHeld)
            _answered.RemoveAt(0);
    }
}
