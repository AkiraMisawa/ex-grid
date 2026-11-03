using ExGrid.Selection;

namespace ExGrid.Finding;

/// <summary>
/// A Find All (ADR-0470 §2): what a Find step would look for, over every row, and the range of the list the grid
/// wants now. The grid asks for the range it shows, a page at a time, under the Row Sequence Version it read the
/// positions in; the source answers that range and the exact count of the whole list.
/// <see cref="GridFindAll.Matches{TRow}"/> is the reference for the list and its order.
/// </summary>
public sealed record GridFindAllRequest
{
    /// <summary>What to look for. Never empty — an empty field asks nothing.</summary>
    public required string Text { get; init; }

    /// <summary>Off: compared <c>OrdinalIgnoreCase</c>, ADR-0023's rule. On: <c>Ordinal</c>.</summary>
    public bool MatchCase { get; init; }

    /// <summary>Off: the cell's displayed text contains <see cref="Text"/>. On: it equals it.</summary>
    public bool WholeCell { get; init; }

    /// <summary>The selected ranges, when more than one cell is selected, as a Find step's. Null: every row.</summary>
    public IReadOnlyList<SelectionRange>? Scope { get; init; }

    /// <summary>The visible columns' names in the grid's current order: position <c>i</c> here is column <c>i</c> of
    /// every position in this request and its answer.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The order the positions above were read in (ADR-0011). An answer under another version is discarded,
    /// and the list with it.</summary>
    public required int RowSequenceVersion { get; init; }

    /// <summary>The first entry of the list wanted, counted from 0 in walk order.</summary>
    public required int Start { get; init; }

    /// <summary>How many entries are wanted from <see cref="Start"/>: the answer holds this many, or every one left
    /// past it.</summary>
    public required int Count { get; init; }

    /// <summary>What this list looks for: the text and the two options, as a step's pattern.</summary>
    public GridFindPattern Pattern => new(Text, MatchCase, WholeCell);
}
