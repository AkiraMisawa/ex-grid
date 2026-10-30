using System.Text.Json.Serialization;

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
    /// <remarks>Also the constructor <c>System.Text.Json</c> reads a range back through, so a
    /// Query that crossed to a server names the rows it named here, and a range no grid can ask
    /// for is refused on the way in as it is here (ADR-0002, FN-13). Without it the reader took
    /// the struct's default and wrote nothing into its get-only properties: every range came
    /// back as no rows at position 0.</remarks>
    [JsonConstructor]
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
