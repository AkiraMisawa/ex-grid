using ExGrid.Finding;

namespace ExGrid.Components;

/// <summary>
/// What a row judges its painted cells by for the Match Mark (ADR-0332 §4): the match function
/// and what the find panel holds. The grid makes one while the panel holds a search and someone can
/// judge it, and replaces it only when the text, an option or the function changes, so its identity
/// is the change signal, as a row instance's is (ADR-0003). A row handed a new one judges its painted
/// cells again and renders only if what it marks moved.
///
/// <para>Not the contract (ADR-0201): public only because <see cref="ExGridRow{TRow}"/>'s parameter
/// names it, hidden from a Consumer's editor, and may change in any version. A Consumer neither makes
/// nor reads one: it passes <c>FindMatch</c>.</para>
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class MatchMarks<TRow>
    where TRow : class
{
    private readonly FindMatchOf<TRow> _match;
    private readonly GridFindPattern _pattern;

    internal MatchMarks(FindMatchOf<TRow> match, GridFindPattern pattern)
    {
        _match = match;
        _pattern = pattern;
    }

    /// <summary>Whether this judges as <paramref name="match"/> would over the text and options the
    /// panel holds: then the grid keeps this instance, every row skips, and no pattern is made to
    /// find that out.</summary>
    internal bool Same(FindMatchOf<TRow> match, string text, bool matchCase, bool wholeCell)
        => ReferenceEquals(_match, match)
            && string.Equals(_pattern.Text, text, StringComparison.Ordinal)
            && _pattern.MatchCase == matchCase
            && _pattern.WholeCell == wholeCell;

    /// <summary>Whether a painted cell matches. Only a cell Find can land on is asked: a value or a
    /// Template cell, whose column is queryable and has displayed text that
    /// <c>GridSource.From</c> searches, so a step and the mark agree. An Action or Mark cell has no
    /// text to search, and is never asked and never marked.</summary>
    internal bool Matches(TRow row, GridColumn<TRow> column)
        => column.IsQueryable && _match(row, column, _pattern);
}
