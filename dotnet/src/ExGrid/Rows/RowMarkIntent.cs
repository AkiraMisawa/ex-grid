namespace ExGrid.Rows;

/// <summary>
/// One marking gesture, as the grid reports it (ADR-0043). Three shapes, one per
/// gesture; none is ever one notification per row.
/// </summary>
public abstract record RowMarkIntent<TRow>
{
    private RowMarkIntent()
    {
    }

    /// <summary>One row's checkbox was pressed. The row is the instance the grid painted
    /// — its identity, as an Edit Intent carries it (ADR-0007).</summary>
    /// <param name="Row">The row whose checkbox was pressed.</param>
    /// <param name="Marked">The state it asks for.</param>
    public sealed record OneRow(TRow Row, bool Marked) : RowMarkIntent<TRow>;

    /// <summary>
    /// Every Detail row of the current result, after filtering: marked or unmarked
    /// together. Marking takes the result <b>as it stands now</b> — a row joining it later
    /// is not marked (ADR-0043). Unmarking clears the current result's marks and leaves
    /// the marks outside it alone.
    /// </summary>
    /// <param name="Marked">True for "mark all", false for "unmark all".</param>
    /// <param name="RowSequenceVersion">The order the header was pressed under — the
    /// moment "the result as it stands" refers to.</param>
    public sealed record AllRows(bool Marked, int RowSequenceVersion) : RowMarkIntent<TRow>;

    /// <summary>
    /// Space over a selection in the Mark Column, or the header under a pager: rows named
    /// by position, because past the Window the grid has no identity to name them by —
    /// the shape of a bulk paste (ADR-0014). The rows are brought into line with
    /// <see cref="RowMarkRules.LineUp"/>. <b>Refuse them under any other
    /// <paramref name="RowSequenceVersion"/></b>: the positions would name other rows.
    /// </summary>
    /// <param name="Ranges">The positions covered, in the whole result.</param>
    /// <param name="RowSequenceVersion">The order the positions were taken in.</param>
    public sealed record Positions(IReadOnlyList<RowRange> Ranges, int RowSequenceVersion) : RowMarkIntent<TRow>;
}
