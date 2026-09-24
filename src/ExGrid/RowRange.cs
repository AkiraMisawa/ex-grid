namespace ExGrid;

/// <summary>
/// The contiguous range a Range Request names (ADR-0001): a non-empty run of rows at
/// non-negative positions. A range beyond the currently known rows is legal — requests
/// race with data updates, and answering nothing is the correct answer — but a negative
/// or empty range is a caller bug and is refused, so every Grid Source implementation
/// faces the same, already-validated shape.
/// </summary>
public readonly record struct RowRange
{
    /// <summary>A run of <paramref name="count"/> rows from position
    /// <paramref name="start"/>. A negative start or a count below one is refused.</summary>
    public RowRange(int start, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Start = start;
        Count = count;
    }

    /// <summary>The first row's position in the whole result, 0-based.</summary>
    public int Start { get; }

    /// <summary>How many rows the range holds — at least one.</summary>
    public int Count { get; }
}
