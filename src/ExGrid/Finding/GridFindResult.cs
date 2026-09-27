namespace ExGrid.Finding;

/// <summary>
/// A Find step's answer (ADR-0047): a position — a row in the current order and a column
/// name — or <see cref="NotFound"/>.
/// </summary>
public sealed class GridFindResult
{
    private GridFindResult(bool isFound, int row, string? column)
    {
        IsFound = isFound;
        Row = row;
        Column = column;
    }

    /// <summary>Nothing matches anywhere the step looked.</summary>
    public static GridFindResult NotFound { get; } = new(false, -1, null);

    /// <summary>The next match: <paramref name="row"/> in the order the request was read in,
    /// <paramref name="column"/> one of the request's column names.</summary>
    public static GridFindResult Found(int row, string column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentException.ThrowIfNullOrEmpty(column);
        return new(true, row, column);
    }

    /// <summary>Whether a match was found.</summary>
    public bool IsFound { get; }

    /// <summary>The matching row, by position. -1 when not found.</summary>
    public int Row { get; }

    /// <summary>The matching column's name. Null when not found.</summary>
    public string? Column { get; }
}
