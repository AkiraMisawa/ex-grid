using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// The Match Mark (ADR-0332 §4): while the find panel holds a search, every painted cell that
// matches it is marked, and the current match — the Focus a found step moved to — apart. The grid
// judges the painted cells itself, with a match function, so the mark follows the scroll with no
// round trip; only the painted rows are known, so nothing is counted. The rows judge their own
// cells and paint a class, as they ask for Cell State (ADR-0006); a row is handed the function and
// the pattern as one immutable instance, replaced only when one of them changes, and renders for a
// new one only where its marks moved (ADR-0003). Nothing per cell reaches JavaScript (ADR-0027).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// The match function for the Match Mark (ADR-0332 §4): whether a painted cell matches what the
    /// find panel holds. While the panel holds a search, every painted value cell it says matches
    /// is marked with the class <c>ex-match-mark</c>, and the current match — the Focus a found step
    /// moved to — with <c>ex-match-mark-current</c> as well; a row scrolled into view is marked as it
    /// is painted, and the marks go when the panel closes or its text is emptied. Only the painted
    /// cells are judged, so nothing is counted.
    ///
    /// <para><b>Null, the default, is the reference rule where a Grid Source answers Find</b>
    /// (<see cref="GridFind.Matches(string, GridFindPattern)"/> over the column's displayed text,
    /// the rule <c>GridSource.From</c> finds by), <b>and nothing where <see cref="OnFind"/> does</b>:
    /// an answer of the Consumer's own may judge by another rule, and a mark the next step does not
    /// reach would be quietly wrong. A Consumer that answers Find itself passes the rule it answers
    /// by to have the marks. The colours are the Visual Tokens <c>--ex-match-mark-fill</c> and
    /// <c>--ex-match-mark-current-fill</c>, never parameters (ADR-0027).</para>
    ///
    /// <para>Hold it in a field: its identity is the change signal, as <see cref="CellState"/>'s
    /// is (ADR-0003/0006), and a new delegate makes every painted row judge its cells again.</para>
    /// </summary>
    [Parameter] public FindMatchOf<TRow>? FindMatch { get; set; }

    // The reference rule, one instance per row type, so the grid's own default never reads as a
    // changed function (ADR-0003).
    private static readonly FindMatchOf<TRow> ReferenceMatch =
        static (row, column, pattern) => GridFind.Matches(column.Info.TextOf(row), pattern);

    // What the rows were last handed, kept while the search, the options and the function stand.
    private MatchMarks<TRow>? _matchMarks;

    // The cell a found step moved the Focus to: the current match while the Focus stays on it.
    private CellPosition? _findCurrent;

    /// <summary>Who judges a painted cell: the Consumer's function, else the reference rule where a
    /// Source answers Find, else nobody (ADR-0332 §4).</summary>
    private FindMatchOf<TRow>? FindMatchRule()
        => FindMatch ?? (OnFind is null && Source is { CanFind: true } ? ReferenceMatch : null);

    /// <summary>What the rows judge their cells by in this render, or null while the panel is closed,
    /// holds no text, or nobody can judge — the same instance while nothing it holds changed.</summary>
    private MatchMarks<TRow>? MatchMarksNow()
    {
        if (!_findOpen || _findText.Length == 0 || FindMatchRule() is not { } rule)
            return _matchMarks = null;
        var pattern = new GridFindPattern(_findText, _findMatchCase, _findWholeCell);
        return _matchMarks is { } held && held.Same(rule, pattern)
            ? held
            : _matchMarks = new MatchMarks<TRow>(rule, pattern);
    }

    /// <summary>The column of the current match in <paramref name="row"/>, or -1: every row but the
    /// one the Focus is in is handed -1, so none of them renders when a step moves on.</summary>
    private int CurrentMatchAt(int row, MatchMarks<TRow>? marks)
    {
        if (marks is null || _findCurrent is not { } current || current.Row != row)
            return -1;
        var selection = _selection.Selection;
        return !selection.IsEmpty && selection.Focus == current ? current.Column : -1;
    }

    /// <summary>A substituted panel's change to what the panel holds (ADR-0055): the grid renders
    /// for it where the marks may move, as it does for the built-in panel's own fields.</summary>
    private void FindHeldChanged()
    {
        if (_findOpen && FindMatchRule() is not null)
            StateHasChanged();
    }
}
