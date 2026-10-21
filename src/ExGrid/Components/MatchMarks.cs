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

    /// <summary>Whether this judges as <paramref name="match"/> would over <paramref name="pattern"/>:
    /// then the grid keeps this instance, and every row skips.</summary>
    internal bool Same(FindMatchOf<TRow> match, GridFindPattern pattern)
        => ReferenceEquals(_match, match) && _pattern == pattern;

    /// <summary>Whether a painted value cell matches. An Action or Template cell paints no value, so
    /// is never asked and never marked.</summary>
    internal bool Matches(TRow row, GridColumn<TRow> column)
        => column.PaintsValue && _match(row, column, _pattern);
}
