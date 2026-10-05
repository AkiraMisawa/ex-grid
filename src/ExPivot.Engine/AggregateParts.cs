global using CountsPart = ExGrid.Data.AggregateCounts;
global using ExtremesPart = ExGrid.Data.AggregateExtremes;
global using SumPart = ExGrid.Data.AggregateSum;
global using VariancePart = ExGrid.Data.AggregateVariance;

using ExGrid.Data;

namespace ExPivot.Engine;

// The parts a Leaf Aggregate is made of, and the arithmetic over them, are the family's one
// definition of the Aggregations, in ExGrid.Data (ADR-0130): ExGrid's Selection Summary reads the
// same. The engine keeps its parts column by column (PartColumns) under its own names.

internal static class PivotAggregations
{
    /// <summary>The shared Aggregation a Value Field's means: the same eleven, in the same order.</summary>
    public static Aggregation ToAggregation(this PivotAggregation aggregation) => aggregation switch
    {
        PivotAggregation.Sum => Aggregation.Sum,
        PivotAggregation.Count => Aggregation.Count,
        PivotAggregation.Average => Aggregation.Average,
        PivotAggregation.Max => Aggregation.Max,
        PivotAggregation.Min => Aggregation.Min,
        PivotAggregation.Product => Aggregation.Product,
        PivotAggregation.CountNumbers => Aggregation.CountNumbers,
        PivotAggregation.StdDev => Aggregation.StdDev,
        PivotAggregation.StdDevp => Aggregation.StdDevp,
        PivotAggregation.Var => Aggregation.Var,
        PivotAggregation.Varp => Aggregation.Varp,
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown PivotAggregation."),
    };
}
