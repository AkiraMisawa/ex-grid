namespace ExGrid.Finding;

/// <summary>
/// The Consumer's answer to a <see cref="GridReplaceRequest"/> (ADR-0332 §3): how many cells it
/// replaced, or a refusal naming every match that could not be written — in which case it replaced
/// nothing, since a Replace All is all or nothing. Either is answered under the Row Sequence Version
/// the Consumer read its rows in; when that is not the request's, the grid refuses the answer as the
/// order having moved (<see cref="FindRefusalReason.OrderChanged"/>), and the Consumer must have
/// written nothing.
/// </summary>
public sealed class GridReplaceResult
{
    private GridReplaceResult(int count, IReadOnlyList<GridReplaceRefusedCell> refused, int rowSequenceVersion)
    {
        Count = count;
        RefusedCells = refused;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>Every match was replaced, as one write — one step on the Consumer's undo stack.</summary>
    /// <param name="count">How many cells were replaced; 0 where nothing matched.</param>
    /// <param name="rowSequenceVersion">The Row Sequence Version the rows were read in before the
    /// write: the request's, or the answer is refused.</param>
    public static GridReplaceResult Replaced(int count, int rowSequenceVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(count, [], rowSequenceVersion);
    }

    /// <summary>Nothing was replaced, because the matches named could not be written.</summary>
    /// <param name="cells">Every match that could not be written, and why. Never empty: a refusal that
    /// names nothing is the silence ADR-0034 rules out.</param>
    /// <param name="rowSequenceVersion">The Row Sequence Version the cells' rows are positions in.</param>
    public static GridReplaceResult Refused(IReadOnlyList<GridReplaceRefusedCell> cells, int rowSequenceVersion)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0)
            throw new ArgumentException("A refused Replace All names the cells that could not be written (ADR-0332 §2).", nameof(cells));
        return new(0, [.. cells], rowSequenceVersion);
    }

    /// <summary>Whether the Consumer replaced the matches (<see cref="Count"/> of them), rather than
    /// refusing.</summary>
    public bool IsReplaced => RefusedCells.Count == 0;

    /// <summary>How many cells were replaced; 0 when refused.</summary>
    public int Count { get; }

    /// <summary>The matches that could not be written; empty when replaced.</summary>
    public IReadOnlyList<GridReplaceRefusedCell> RefusedCells { get; }

    /// <summary>The Row Sequence Version the answer was made under.</summary>
    public int RowSequenceVersion { get; }
}

/// <summary>
/// One match a Replace could not write (ADR-0332 §2), named by its place: a row in the order the
/// request was read in and a column name.
/// </summary>
/// <param name="Row">The row, by position in the request's order.</param>
/// <param name="Column">The column's name, one of the request's <see cref="GridReplaceRequest.Columns"/>.</param>
/// <param name="Message">Why, in the Consumer's words — the reason its write refused, as an Edit
/// Verdict's Reject gives one. Null: the column is not Editable, which Chrome words itself.</param>
public sealed record GridReplaceRefusedCell(int Row, string Column, string? Message)
{
    /// <summary>Whether the match could not be written because its column is not Editable.</summary>
    public bool NotEditable => Message is null;
}
