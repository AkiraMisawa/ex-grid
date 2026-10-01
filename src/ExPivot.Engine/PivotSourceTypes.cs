using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// What a Pivot Source can do (ADR-0065): the Aggregations it answers — Value Field Settings…
/// offers the others disabled, with the reason, and ExPivot never asks a source for one — and
/// whether it can be asked again for newer data (Excel's Refresh).
/// </summary>
public sealed class PivotSourceFeatures
{
    private readonly HashSet<PivotAggregation> _offered;

    /// <summary>Declares a source's features.</summary>
    /// <param name="aggregations">The Aggregations it answers.</param>
    /// <param name="canRefresh">Whether it can be refreshed; the toolbar then offers Refresh.</param>
    public PivotSourceFeatures(IEnumerable<PivotAggregation> aggregations, bool canRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(aggregations);
        _offered = [];
        foreach (var aggregation in aggregations)
        {
            if (!Enum.IsDefined(aggregation))
                throw new ArgumentOutOfRangeException(nameof(aggregations), aggregation, "Unknown PivotAggregation.");
            _offered.Add(aggregation);
        }
        Aggregations = Enum.GetValues<PivotAggregation>().Where(_offered.Contains).ToArray();
        CanRefresh = canRefresh;
    }

    /// <summary>Every Aggregation, and no Refresh — the bundled source's features: it is refreshed
    /// by handing ExPivot a new source (ADR-0065).</summary>
    public static PivotSourceFeatures All { get; } = new(Enum.GetValues<PivotAggregation>());

    /// <summary>The Aggregations the source answers, in the enum's order.</summary>
    public IReadOnlyList<PivotAggregation> Aggregations { get; }

    /// <summary>Whether the source can be refreshed.</summary>
    public bool CanRefresh { get; }

    /// <summary>Whether the source answers <paramref name="aggregation"/>.</summary>
    public bool Offers(PivotAggregation aggregation) => _offered.Contains(aggregation);

    /// <summary>The Aggregations that read <paramref name="part"/> — one part, not a combination —
    /// and none of which the source offers; empty when it offers one. A question may ask for a
    /// part only when one of them is offered.</summary>
    internal PivotAggregation[] MissingFor(PivotParts part)
    {
        var readers = Enum.GetValues<PivotAggregation>().Where(a => PivotQuery.PartsOf(a) == part).ToArray();
        return readers.Any(Offers) ? [] : readers;
    }
}

/// <summary>
/// A Pivot Source's notice that its data moved on (ADR-0065/0066): ExPivot asks again for the
/// whole answer. Serialisable (<see cref="PivotJson"/>), so a server's push can carry it.
/// </summary>
/// <param name="SourceVersion">The data's new Source Version, or null when the source does not
/// know it yet — the next answer carries it.</param>
public sealed record PivotSourceChanged(string? SourceVersion = null);

/// <summary>Why a Pivot Source refused a question (ADR-0065).</summary>
public enum PivotSourceRefusalKind
{
    /// <summary>The answer would need more leaves than the question allows: "this layout needs
    /// more than 200,000 cells". <see cref="PivotSourceRefusal.Limit"/> holds the cap.</summary>
    TooManyLeaves = 0,

    /// <summary>The question names a field the source does not have.
    /// <see cref="PivotSourceRefusal.Field"/> names it.</summary>
    UnknownField,

    /// <summary>The question asks for a part that only Aggregations the source does not offer
    /// read. A source is never obliged to approximate an Aggregation.</summary>
    AggregationNotOffered,

    /// <summary>The question is asked under a Source Version the source can no longer answer
    /// under: the data has changed, and the records would not add up to the report.</summary>
    SourceVersionNotHeld,
}

/// <summary>
/// A Pivot Source's refusal to answer (ADR-0065): its kind, a sentence a person can read, and the
/// field or the cap it concerns. A refusal is an answer, not a failure: the report stays on the
/// layout it had and says why (principle 5 puts the cap on what cannot be executed).
/// </summary>
/// <param name="Kind">Why the source refused.</param>
/// <param name="Message">What a person reads.</param>
public sealed record PivotSourceRefusal(PivotSourceRefusalKind Kind, string Message)
{
    /// <summary>Why the source refused.</summary>
    public PivotSourceRefusalKind Kind { get; } = Enum.IsDefined(Kind)
        ? Kind
        : throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown PivotSourceRefusalKind.");

    /// <summary>What a person reads.</summary>
    public string Message { get; } = !string.IsNullOrEmpty(Message)
        ? Message
        : throw new ArgumentException("A refusal says why.", nameof(Message));

    /// <summary>The field the refusal concerns, or null.</summary>
    public string? Field { get; init; }

    /// <summary>The cap a <see cref="PivotSourceRefusalKind.TooManyLeaves"/> refusal passed, or null.</summary>
    public long? Limit { get; init; }

    /// <summary>"This layout needs more than 200,000 cells." — the refusal of a question whose
    /// answer would pass <paramref name="maxLeaves"/> (ADR-0065).</summary>
    public static PivotSourceRefusal TooManyLeaves(int maxLeaves)
        => new(PivotSourceRefusalKind.TooManyLeaves,
            $"This layout needs more than {maxLeaves.ToString("N0", CultureInfo.InvariantCulture)} cells.")
        {
            Limit = maxLeaves,
        };

    /// <summary>The refusal of a question naming a field the source does not have.</summary>
    public static PivotSourceRefusal UnknownField(string field)
        => new(PivotSourceRefusalKind.UnknownField, $"The source has no field named '{field}'.") { Field = field };

    /// <summary>The refusal of a question asking for a part of <paramref name="field"/> that only
    /// the given Aggregations, none of which the source offers, read.</summary>
    public static PivotSourceRefusal AggregationNotOffered(string field, IReadOnlyList<PivotAggregation> aggregations)
        => new(PivotSourceRefusalKind.AggregationNotOffered,
            $"The source does not offer {string.Join(" or ", aggregations)}, which the question asks of '{field}'.")
        {
            Field = field,
        };

    /// <summary>The refusal of a question asked under a Source Version the source no longer
    /// holds.</summary>
    public static PivotSourceRefusal SourceVersionNotHeld(string sourceVersion)
        => new(PivotSourceRefusalKind.SourceVersionNotHeld,
            $"The data has changed since version '{sourceVersion}'; refresh the report.");
}

/// <summary>
/// How the bundled source shares the thread while it reads the records and assembles its answer,
/// and how ExPivot shares it while it makes an answer's cube and lays out its report (ADR-0065,
/// PV-27/PV-40): the work runs in slices of about <see cref="Budget"/> and yields between them, so
/// a browser keeps painting, and cancelled work stops at the next slice. Every setting can be
/// replaced — a test makes the slices deterministic with a zero budget and a yield of its own.
/// </summary>
public sealed record PivotSlicing
{
    private readonly TimeSpan _budget = TimeSpan.FromMilliseconds(30);
    private readonly int _recordsPerCheck = 1024;
    private readonly int _unitsPerCheck = 1024;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    /// <summary>30 ms slices, the clock read every 1,024 records, and the platform's yield.</summary>
    public static PivotSlicing Default { get; } = new();

    /// <summary>How long a slice runs before it yields: 30 ms by default, so that a slice stays
    /// within the 50 ms the Definition of Done's targets allow. Zero yields at every check.</summary>
    public TimeSpan Budget
    {
        get => _budget;
        init => _budget = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(Budget), value, "A budget is not negative.");
    }

    /// <summary>How many records are read between two looks at the clock; 1,024 by default.</summary>
    public int RecordsPerCheck
    {
        get => _recordsPerCheck;
        init => _recordsPerCheck = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(RecordsPerCheck), value, "At least one record is read between two looks at the clock.");
    }

    /// <summary>The clock a slice is measured by.</summary>
    public TimeProvider TimeProvider
    {
        get => _timeProvider;
        init => _timeProvider = value ?? throw new ArgumentNullException(nameof(TimeProvider));
    }

    /// <summary>What runs between two slices; null for the platform's own: a yield that captures no
    /// caller's context, which in a browser is a turn of the page's event loop, in which it
    /// paints.</summary>
    public Func<CancellationToken, ValueTask>? Yield { get; init; }

    /// <summary>Told how many rows each step of a question read — for layer 1, which holds a
    /// question to the rows it reads (a cancelled one stops at the next slice; one refused for its
    /// leaves stops at the row that passed the cap).</summary>
    internal Action<long>? RowsRead { get; init; }

    /// <summary>
    /// How many units of the work after a pass — a leaf of an answer assembled, a leaf's cells of a
    /// cube merged, a row of a report laid out — are done between two looks at the clock; 1,024 by
    /// default. The units are counted across every step of the work, so a small answer never reads
    /// the clock at all. Layer 1 lowers it to slice small answers.
    /// </summary>
    internal int UnitsPerCheck
    {
        get => _unitsPerCheck;
        init => _unitsPerCheck = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(UnitsPerCheck), value, "At least one unit is done between two looks at the clock.");
    }

    /// <summary>
    /// Yields the thread as a slice ends: <see cref="Yield"/> when it is set, otherwise the
    /// platform's, capturing no caller's context. In a browser that is a turn of the page's event
    /// loop, in which it paints: one frame a slice, at under a millisecond a yield, where a delay
    /// of 1 ms cost 4.3–4.4 ms (ExGrid.Data's ticket 07 measured both). Work that goes on after a piece of sliced work
    /// has yielded takes a slice of its own with it (ExPivot does, between making the cube and
    /// laying out the report).
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public ValueTask YieldAsync(CancellationToken cancellationToken = default)
        => Yield is { } yield ? yield(cancellationToken) : PlatformYield(cancellationToken);

    // The bundled source captures no caller's context: its slices go on wherever the runtime puts
    // them, so a caller that blocks on a question — on a UI thread, say — never waits on itself.
    // The caller looks at its token once the yield returns, as it always has outside a browser.
    private static async ValueTask PlatformYield(CancellationToken cancellationToken)
        => await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
}
