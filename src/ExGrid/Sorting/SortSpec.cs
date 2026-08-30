namespace ExGrid;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// One level of the Sorts list (ADR-0001). The list is in priority order: the first
/// entry is the primary sort. Blanks go last in both directions (ADR-0023).
/// </summary>
public sealed record SortSpec(string Column, SortDirection Direction);
