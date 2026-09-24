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
    public RowRange(int start, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Start = start;
        Count = count;
    }

    public int Start { get; }

    public int Count { get; }
}
