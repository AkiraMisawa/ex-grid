using ExGrid.Selection;

namespace ExGrid.Finding;

/// <summary>
/// One step of a Find (ADR-0047): what to look for, where to start, which way, and in what.
/// The grid asks and the Consumer answers, as it answers for sort and filter; the grid only
/// moves the Focus. <see cref="GridFind.Step{TRow}"/> is the reference for what a match is.
/// </summary>
public sealed record GridFindRequest
{
    /// <summary>What to look for. Never empty — an empty field asks nothing.</summary>
    public required string Text { get; init; }

    /// <summary>Off: compared <c>OrdinalIgnoreCase</c>, ADR-0023's rule. On: <c>Ordinal</c>.</summary>
    public bool MatchCase { get; init; }

    /// <summary>Off: the cell's displayed text contains <see cref="Text"/>. On: it equals it.</summary>
    public bool WholeCell { get; init; }

    /// <summary>Whether this step runs backward — "previous", Shift+Enter.</summary>
    public bool Backward { get; init; }

    /// <summary>The Focus the step starts from: the search begins at the cell after it
    /// (before it, backward) and considers it last, so a lone match finds itself. Null:
    /// from the start (the end, backward).</summary>
    public CellPosition? From { get; init; }

    /// <summary>The selected ranges, when more than one cell is selected — Excel's "search the
    /// selection". Null: every row. A cell covered by two ranges counts once.</summary>
    public IReadOnlyList<SelectionRange>? Scope { get; init; }

    /// <summary>The visible columns' names in the grid's current order: position <c>i</c> here
    /// is column <c>i</c> of every <see cref="CellPosition"/> in this request. The Consumer
    /// does not otherwise know the order the user dragged, or which columns are hidden.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The order the positions above were read in (ADR-0011). An answer the grid
    /// receives under another version is discarded.</summary>
    public required int RowSequenceVersion { get; init; }
}
