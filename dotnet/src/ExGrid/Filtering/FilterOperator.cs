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
    /// <summary>The value equals the operand — case-insensitively for text, and with no
    /// truncation to the day for a date (ADR-0023). Offered on every column type.</summary>
    Equals,
    /// <summary>The value differs from the operand. A Blank does not match (ADR-0023).
    /// Not offered on Boolean columns.</summary>
    NotEquals,
    /// <summary>The value is greater than the operand. Number and Date columns only.</summary>
    GreaterThan,
    /// <summary>The value is greater than or equal to the operand. Number and Date
    /// columns only.</summary>
    GreaterThanOrEqual,
    /// <summary>The value is less than the operand. Number and Date columns only.</summary>
    LessThan,
    /// <summary>The value is less than or equal to the operand. Number and Date columns
    /// only.</summary>
    LessThanOrEqual,
    /// <summary>The text contains the operand, case-insensitively (ADR-0023). Text
    /// columns only.</summary>
    Contains,
    /// <summary>The text does not contain the operand. A Blank does not match
    /// (ADR-0023). Text columns only.</summary>
    DoesNotContain,
    /// <summary>The text starts with the operand, case-insensitively. Text columns
    /// only.</summary>
    StartsWith,
    /// <summary>The text ends with the operand, case-insensitively. Text columns
    /// only.</summary>
    EndsWith,
    /// <summary>How the value-list mode of the filter panel serialises (ADR-0009).</summary>
    In,
    /// <summary>The only operator a Blank matches (ADR-0023).</summary>
    IsBlank,
    /// <summary>The value is not a Blank. Takes no operand (ADR-0023).</summary>
    IsNotBlank,
}
