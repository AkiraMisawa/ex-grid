namespace ExGrid.Rows;

/// <summary>
/// What the Consumer answers about its Row Marks for the header and the count display
/// (ADR-0043). The grid cannot count them itself: it holds a Window, and a Window whose
/// rows are all marked says nothing about the million rows beyond it.
/// </summary>
public readonly record struct RowMarkCounts
{
    /// <summary>Counts as the Consumer holds them. Refused when they contradict each
    /// other — more marked rows in the result than the result has — because a header
    /// painted from them would claim something nobody knows.</summary>
    /// <param name="markedInResult">Marked Detail rows of the current result.</param>
    /// <param name="rowsInResult">Detail rows of the current result.</param>
    /// <param name="markedOutsideResult">Marked rows the current filter leaves out.</param>
    public RowMarkCounts(int markedInResult, int rowsInResult, int markedOutsideResult)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(markedInResult);
        ArgumentOutOfRangeException.ThrowIfNegative(rowsInResult);
        ArgumentOutOfRangeException.ThrowIfNegative(markedOutsideResult);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(markedInResult, rowsInResult);
        MarkedInResult = markedInResult;
        RowsInResult = rowsInResult;
        MarkedOutsideResult = markedOutsideResult;
    }

    /// <summary>Marked Detail rows of the current result — after filtering.</summary>
    public int MarkedInResult { get; }

    /// <summary>Detail rows of the current result. Group and Total rows are never
    /// marked, so they are never counted (ADR-0024/0043).</summary>
    public int RowsInResult { get; }

    /// <summary>Marked rows the current filter leaves out — said aloud beside the count,
    /// so an action never lands on rows the user cannot see without being told
    /// (ADR-0043).</summary>
    public int MarkedOutsideResult { get; }

    /// <summary>Every mark, inside the result and outside it.</summary>
    public int Marked => MarkedInResult + MarkedOutsideResult;
}
