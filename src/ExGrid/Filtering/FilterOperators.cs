namespace ExGrid;

/// <summary>
/// Which operators a column offers is decided by the core from the declared type,
/// not by Chrome and not by the Consumer (ADR-0009, ADR-0023).
/// </summary>
public static class FilterOperators
{
    private static readonly FilterOperator[] Text =
    [
        FilterOperator.Equals, FilterOperator.NotEquals,
        FilterOperator.Contains, FilterOperator.DoesNotContain,
        FilterOperator.StartsWith, FilterOperator.EndsWith,
        FilterOperator.In, FilterOperator.IsBlank, FilterOperator.IsNotBlank,
    ];

    private static readonly FilterOperator[] Comparable =
    [
        FilterOperator.Equals, FilterOperator.NotEquals,
        FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
        FilterOperator.In, FilterOperator.IsBlank, FilterOperator.IsNotBlank,
    ];

    private static readonly FilterOperator[] Boolean =
    [
        // In stays available: the value-list panel mode serialises as In on every
        // column type, Boolean included (ADR-0009, ADR-0023).
        FilterOperator.Equals, FilterOperator.In,
        FilterOperator.IsBlank, FilterOperator.IsNotBlank,
    ];

    public static IReadOnlyList<FilterOperator> AllowedFor(ColumnType type) => type switch
    {
        ColumnType.Text => Text,
        ColumnType.Number => Comparable,
        ColumnType.Date => Comparable,
        ColumnType.Boolean => Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
