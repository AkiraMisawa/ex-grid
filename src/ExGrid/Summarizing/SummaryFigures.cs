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

/// <summary>The figures one at a time, in the order Excel's status bar shows them.</summary>
public static class SummaryFigureOrder
{
    /// <summary>Average, Count, Numerical Count, Min, Max, Sum.</summary>
    public static IReadOnlyList<SummaryFigures> Each { get; } =
    [
        SummaryFigures.Average, SummaryFigures.Count, SummaryFigures.NumericalCount,
        SummaryFigures.Min, SummaryFigures.Max, SummaryFigures.Sum,
    ];

    /// <summary>Whether a single figure counts cells — Count and Numerical Count — rather than reading
    /// their numbers.</summary>
    public static bool IsCount(SummaryFigures figure) => figure is SummaryFigures.Count or SummaryFigures.NumericalCount;

    /// <summary>The Aggregation a single figure means.</summary>
    public static ExGrid.Data.Aggregation AggregationOf(SummaryFigures figure) => figure switch
    {
        SummaryFigures.Average => ExGrid.Data.Aggregation.Average,
        SummaryFigures.Count => ExGrid.Data.Aggregation.Count,
        SummaryFigures.NumericalCount => ExGrid.Data.Aggregation.CountNumbers,
        SummaryFigures.Min => ExGrid.Data.Aggregation.Min,
        SummaryFigures.Max => ExGrid.Data.Aggregation.Max,
        SummaryFigures.Sum => ExGrid.Data.Aggregation.Sum,
        _ => throw new ArgumentOutOfRangeException(nameof(figure), figure, "One figure, not a combination."),
    };
}
