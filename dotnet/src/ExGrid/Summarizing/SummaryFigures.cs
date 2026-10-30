namespace ExGrid.Summarizing;

/// <summary>
/// The figures of a Selection Summary — Excel's status-bar figures (ADR-0130). Each means what the
/// Aggregation of the same name means (<see cref="ExGrid.Data.Aggregation"/>). Which are shown is
/// the Consumer's state; a request asks only for those.
/// </summary>
[Flags]
public enum SummaryFigures
{
    /// <summary>No figure.</summary>
    None = 0,

    /// <summary>The mean of the numbers.</summary>
    Average = 1,

    /// <summary>How many cells are not blank, numbers or not — Excel's "Count".</summary>
    Count = 2,

    /// <summary>How many cells hold numbers — Excel's "Numerical Count".</summary>
    NumericalCount = 4,

    /// <summary>The smallest number.</summary>
    Min = 8,

    /// <summary>The largest number.</summary>
    Max = 16,

    /// <summary>The sum of the numbers.</summary>
    Sum = 32,

    /// <summary>Excel's default: Average, Count and Sum.</summary>
    Default = Average | Count | Sum,

    /// <summary>All six.</summary>
    All = Average | Count | NumericalCount | Min | Max | Sum,
}

/// <summary>
/// The figures one at a time, in the order Excel's status bar shows them, and what each one is: the
/// id of its name, the Aggregation it means, and whether it counts cells rather than reading their
/// numbers. One table, which every place that asks about a figure reads (ADR-0130).
/// </summary>
public static class SummaryFigureOrder
{
    private sealed record Entry(SummaryFigures Figure, string LabelId, ExGrid.Data.Aggregation Aggregation, bool IsCount);

    private static readonly Entry[] Table =
    [
        new(SummaryFigures.Average, Chrome.SummaryLabelIds.Average, ExGrid.Data.Aggregation.Average, false),
        new(SummaryFigures.Count, Chrome.SummaryLabelIds.Count, ExGrid.Data.Aggregation.Count, true),
        new(SummaryFigures.NumericalCount, Chrome.SummaryLabelIds.NumericalCount, ExGrid.Data.Aggregation.CountNumbers, true),
        new(SummaryFigures.Min, Chrome.SummaryLabelIds.Min, ExGrid.Data.Aggregation.Min, false),
        new(SummaryFigures.Max, Chrome.SummaryLabelIds.Max, ExGrid.Data.Aggregation.Max, false),
        new(SummaryFigures.Sum, Chrome.SummaryLabelIds.Sum, ExGrid.Data.Aggregation.Sum, false),
    ];

    /// <summary>Average, Count, Numerical Count, Min, Max, Sum.</summary>
    public static IReadOnlyList<SummaryFigures> Each { get; } = [.. Table.Select(entry => entry.Figure)];

    /// <summary>Whether a single figure counts cells — Count and Numerical Count — rather than reading
    /// their numbers.</summary>
    public static bool IsCount(SummaryFigures figure) => Of(figure).IsCount;

    /// <summary>The Aggregation a single figure means.</summary>
    public static ExGrid.Data.Aggregation AggregationOf(SummaryFigures figure) => Of(figure).Aggregation;

    /// <summary>The id of a single figure's name (<see cref="Chrome.SummaryLabelIds"/>).</summary>
    public static string LabelIdOf(SummaryFigures figure) => Of(figure).LabelId;

    private static Entry Of(SummaryFigures figure)
        => Array.Find(Table, entry => entry.Figure == figure)
            ?? throw new ArgumentOutOfRangeException(nameof(figure), figure, "One figure, not a combination.");
}
