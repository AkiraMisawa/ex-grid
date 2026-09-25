namespace ExGrid;

/// <summary>
/// The declared type of a Column. Declared by the Consumer, never inferred from values
/// (ADR-0002) — it decides which filter operators exist (ADR-0023) and, later, the default
/// format. A value whose runtime type contradicts the declaration is refused, not coerced.
/// </summary>
public enum ColumnType
{
    /// <summary>Values are strings, compared ordinally and case-insensitively
    /// (ADR-0023). A value too wide for its column is cut with an ellipsis (ADR-0016).</summary>
    Text,

    /// <summary>Values are integral, floating-point or decimal numbers, all compared as
    /// <see cref="decimal"/>; one that decimal cannot represent is refused (ADR-0023). A
    /// value too wide for its column becomes <c>####</c> (ADR-0016).</summary>
    Number,

    /// <summary>Values are <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or
    /// <see cref="DateOnly"/> — one of them per column — each compared by its own .NET
    /// semantics, with no truncation to the day (ADR-0023). A value too wide for its
    /// column becomes <c>####</c> (ADR-0016).</summary>
    Date,

    /// <summary>Values are <see cref="bool"/>; false orders before true ascending
    /// (ADR-0023).</summary>
    Boolean,
}
