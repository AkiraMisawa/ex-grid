namespace ExGrid;

/// <summary>
/// How the clauses of one column's filter combine. Or exists only inside a column;
/// columns always combine with And (ADR-0009).
/// </summary>
public enum FilterCombinator
{
    And,
    Or,
}

/// <summary>
/// One condition on one column. <paramref name="Value"/> carries the operand for every
/// operator except <see cref="FilterOperator.In"/>, which uses <paramref name="Values"/>
/// (a null entry there explicitly selects Blanks — ADR-0023). IsBlank / IsNotBlank take
/// neither.
/// </summary>
public sealed record FilterClause(
    FilterOperator Operator,
    object? Value = null,
    IReadOnlyList<object?>? Values = null);

/// <summary>One column's filter: clauses combined with a single combinator (ADR-0009).</summary>
public sealed record FilterSpec(
    IReadOnlyList<FilterClause> Clauses,
    FilterCombinator Combinator = FilterCombinator.And);
