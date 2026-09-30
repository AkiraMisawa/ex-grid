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
/// How the bundled source shares the thread while it reads the records (ADR-0065): it works in
/// slices of about <see cref="Budget"/> and yields between them, so a browser keeps painting, and
/// a cancelled question stops at the next slice. Every setting can be replaced — a test makes the
/// slices deterministic with a zero budget and a yield of its own.
/// </summary>
public sealed record PivotSlicing
{
    private readonly TimeSpan _budget = TimeSpan.FromMilliseconds(30);
    private readonly int _recordsPerCheck = 1024;
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

    /// <summary>What runs between two slices; null for the platform's own: <c>Task.Delay(1)</c> in
    /// a browser, so that it can paint, and <c>Task.Yield()</c> elsewhere.</summary>
    public Func<CancellationToken, ValueTask>? Yield { get; init; }

    internal ValueTask YieldAsync(CancellationToken cancellationToken)
        => Yield is { } yield ? yield(cancellationToken) : PlatformYield(cancellationToken);

    private static async ValueTask PlatformYield(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsBrowser())
            await Task.Delay(1, cancellationToken);
        else
            await Task.Yield();
    }
}
