namespace ExGrid;

/// <summary>
/// The whole Filter: per-column specs keyed by column name, combined with And across
/// columns (ADR-0009), plus the Opaque Filter slot. The Opaque Filter's meaning exists
/// only Consumer-side — the reference implementation ignores it (ADR-0023).
/// </summary>
public sealed record GridFilter(
    IReadOnlyDictionary<string, FilterSpec> Columns,
    object? Opaque = null);
