namespace ExGrid;

/// <summary>
/// The filter operator set. This enum is public API: adding members later is cheap,
/// changing what one means is a breaking change (ADR-0002). The meaning of each member
/// is pinned by the reference implementation (ADR-0023). There is deliberately no
/// Between (two clauses combined with And) and no dynamic date operator (a serialised
/// Query must keep one meaning).
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    DoesNotContain,
    StartsWith,
    EndsWith,
    /// <summary>How the value-list mode of the filter panel serialises (ADR-0009).</summary>
    In,
    /// <summary>The only operator a Blank matches (ADR-0023).</summary>
    IsBlank,
    IsNotBlank,
}
