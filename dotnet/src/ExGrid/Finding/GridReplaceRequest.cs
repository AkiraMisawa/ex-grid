using ExGrid.Selection;

namespace ExGrid.Finding;

/// <summary>
/// A Replace All (ADR-0332 §3): the find request's fields and the replacement, raised once over every
/// match, under the Row Sequence Version it was read in. ExGrid holds no rows (ADR-0001), so only the
/// Consumer can reach every match: it performs the whole write — all of it or none of it — and answers
/// with a <see cref="GridReplaceResult"/>. The grid writes nothing itself.
/// <see cref="GridReplace.Plan{TRow}"/> is the reference for what is replaced, as
/// <see cref="GridFind.Step{TRow}"/> is for what is found.
///
/// <para><b>What is matched is the cell's entry</b> — the text the Cell Editor would open with — not
/// the displayed text Find matches (ADR-0332 §2), compared by Find's rule: the same
/// <see cref="Pattern"/>, the same scope, the same columns.</para>
/// </summary>
public sealed record GridReplaceRequest
{
    /// <summary>What to look for. Never empty — an empty field asks nothing.</summary>
    public required string Text { get; init; }

    /// <summary>Off: compared <c>OrdinalIgnoreCase</c>, ADR-0023's rule. On: <c>Ordinal</c>.</summary>
    public bool MatchCase { get; init; }

    /// <summary>Off: every occurrence of <see cref="Text"/> in the entry is replaced. On: an entry
    /// equal to it is replaced whole, and any other is no match.</summary>
    public bool WholeCell { get; init; }

    /// <summary>What each match is replaced with. May be empty: the matched text is removed.</summary>
    public required string Replacement { get; init; }

    /// <summary>The selected ranges, when more than one cell is selected — Find's "search the
    /// selection", carried over (ADR-0332 §2). Null: every row. A cell covered by two ranges counts
    /// once.</summary>
    public IReadOnlyList<SelectionRange>? Scope { get; init; }

    /// <summary>The visible columns' names in the grid's current order: position <c>i</c> here is
    /// column <c>i</c> of every <see cref="SelectionRange"/> in <see cref="Scope"/>, as in a
    /// <see cref="GridFindRequest"/>.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The names among <see cref="Columns"/> the grid declares Editable (ADR-0035): a write
    /// may land only there. A match in any other column cannot be written, so it refuses the whole
    /// Replace All, naming the cell (ADR-0332 §2).</summary>
    public required IReadOnlyList<string> EditableColumns { get; init; }

    /// <summary>What this request looks for, as a Find step and a Match Mark judge a cell.</summary>
    public GridFindPattern Pattern => new(Text, MatchCase, WholeCell);

    /// <summary>The order <see cref="Scope"/>'s positions were read in (ADR-0011). The Consumer
    /// answers under the version it holds, and the grid refuses an answer under another one as the
    /// order having moved; the Consumer writes nothing then (<see cref="GridReplaceResult"/>).</summary>
    public required int RowSequenceVersion { get; init; }
}
