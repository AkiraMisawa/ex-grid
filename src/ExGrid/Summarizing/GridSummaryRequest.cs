using ExGrid.Selection;

namespace ExGrid.Summarizing;

/// <summary>
/// One question of a Selection Summary (ADR-0130): which cells, read in which order, and which
/// figures. The grid asks and whoever holds the data answers, as for Find (ADR-0055);
/// <see cref="GridSummary.Of{TRow}"/> is the reference for what each figure is.
/// </summary>
public sealed record GridSummaryRequest
{
    /// <summary>The selected ranges, positions in the order <see cref="RowSequenceVersion"/>
    /// names. A cell covered by two ranges counts once.</summary>
    public required IReadOnlyList<SelectionRange> Ranges { get; init; }

    /// <summary>The visible columns' names in the grid's current order: position <c>i</c> here is
    /// column <c>i</c> of every range. Hidden columns are not summarised.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The order the ranges were read in (ADR-0011). An answer the grid receives under
    /// another version is shown nowhere.</summary>
    public required int RowSequenceVersion { get; init; }

    /// <summary>The figures shown, the only ones asked for: an answerer computes these alone.</summary>
    public required SummaryFigures Figures { get; init; }

    /// <summary>The Focus — Excel's active cell — whose format the figures are shown in, as Excel shows
    /// its status bar's. An answerer that formats cells itself (ExSheet, ExPivot) writes each figure's
    /// text by it (<see cref="GridSummaryResult.TextOf"/>). Null when the grid has none.</summary>
    public CellPosition? Focus { get; init; }
}
