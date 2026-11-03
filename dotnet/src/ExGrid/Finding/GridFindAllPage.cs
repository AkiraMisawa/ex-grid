namespace ExGrid.Finding;

/// <summary>One cell of a Find All's list (ADR-0470 §2): where it is, and the text it displays, which is what
/// matched.</summary>
/// <param name="Row">The row's position in the order the request was read in.</param>
/// <param name="Column">The column's name, one of the request's columns.</param>
/// <param name="Text">The cell's displayed text, as the user reads it.</param>
/// <param name="RowLabel">What the list calls the row in the Consumer's words, in place of its position; null, the
/// list names it by its position in the order shown (ADR-0470 §3).</param>
public sealed record GridFindAllMatch(int Row, string Column, string Text, string? RowLabel = null);

/// <summary>
/// A source's answer to a Find All (ADR-0470 §2): the entries of the range asked and the exact count of the whole
/// list, which is never an estimate and never capped, read under one Row Sequence Version.
/// </summary>
public sealed record GridFindAllPage
{
    private GridFindAllPage(int total, IReadOnlyList<GridFindAllMatch> matches, int rowSequenceVersion)
        => (Total, Matches, RowSequenceVersion) = (total, matches, rowSequenceVersion);

    /// <summary>How many cells match, exactly, over every row in the scope.</summary>
    public int Total { get; }

    /// <summary>The entries from the request's start, in walk order.</summary>
    public IReadOnlyList<GridFindAllMatch> Matches { get; }

    /// <summary>The order the rows were read in. Under another than the request's, the grid drops the list as the
    /// order having moved.</summary>
    public int RowSequenceVersion { get; }

    /// <summary>A page: <paramref name="matches"/> of a list of <paramref name="total"/>, read under
    /// <paramref name="rowSequenceVersion"/>. A count below 0, or more matches than the count, is refused.</summary>
    /// <param name="total">How many cells match.</param>
    /// <param name="matches">The entries of the range asked.</param>
    /// <param name="rowSequenceVersion">The order the rows were read in.</param>
    public static GridFindAllPage Of(int total, IReadOnlyList<GridFindAllMatch> matches, int rowSequenceVersion)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (matches.Count > total)
            throw new ArgumentOutOfRangeException(nameof(matches), $"A Find All page holds {matches.Count} matches of a list of {total}.");
        return new(total, [.. matches], rowSequenceVersion);
    }
}
