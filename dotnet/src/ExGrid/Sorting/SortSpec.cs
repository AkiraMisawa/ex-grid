namespace ExGrid;

/// <summary>The direction of one level of the Sorts list. Blanks go last in either
/// direction (ADR-0023).</summary>
public enum SortDirection
{
    /// <summary>Smallest first: A to Z, earliest date first, false before true.</summary>
    Ascending,

    /// <summary>Largest first — the reverse of <see cref="Ascending"/>, except that
    /// Blanks still go last.</summary>
    Descending,
}

/// <summary>
/// One level of the Sorts list (ADR-0001). The list is in priority order: the first
/// entry is the primary sort. Blanks go last in both directions (ADR-0023).
/// </summary>
public sealed record SortSpec(string Column, SortDirection Direction);
