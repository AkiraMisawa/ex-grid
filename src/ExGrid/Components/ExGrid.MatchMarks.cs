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
    /// find panel holds. While the panel holds a search, every painted cell Find can land on — a
    /// value or Template cell, never an Action or Mark cell — that it says matches is marked with
    /// the class <c>ex-match-mark</c>, and the current match — the Focus a found step moved to, for
    /// as long as the Focus stays there — with <c>ex-match-mark-current</c> as well; a row scrolled
    /// into view is marked as it is painted, and the marks go when the panel closes or its text is
    /// emptied. Only the painted cells are judged, so nothing is counted.
    ///
    /// <para><b>Null, the default, is the reference rule where the bound Source is
    /// <c>GridSource.From</c>'s</b> (<see cref="InMemoryGridSource{TRow}"/>, which finds by
    /// <see cref="GridFind.Matches(string, GridFindPattern)"/> over the column's displayed text),
    /// <b>and nothing anywhere else</b>: where <see cref="OnFind"/> answers, or a Source of another
    /// kind does — <c>GridSource.Fetch</c> with a <c>find</c> delegate, or a Source of the
    /// Consumer's own — the answer may judge by another rule, and a mark the next step does not
    /// reach would be quietly wrong. Whoever answers Find by a rule of their own passes it here to
    /// have the marks. The colours are the Visual Tokens <c>--ex-match-mark-fill</c> and
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

    // The cell a found step moved the Focus to, and the order it was found in: the current match
    // while the Focus stays on it, forgotten the moment the Focus leaves it or the order moves, so
    // coming back to that position — or another row arriving there — is not taken for it.
    private CellPosition? _findCurrent;
    private int _findCurrentVersion;

    /// <summary>Who judges a painted cell: the Consumer's function, else the reference rule where
    /// <c>GridSource.From</c>'s Source answers Find by it, else nobody (ADR-0332 §4). OnFind beside a
    /// Source is refused by name (ADR-0055), so a bound in-memory Source is the one answering.</summary>
    private FindMatchOf<TRow>? FindMatchRule()
        => FindMatch ?? (Source is InMemoryGridSource<TRow> ? ReferenceMatch : null);

    /// <summary>What the rows judge their cells by in this render, or null while the panel is closed,
    /// holds no text, or nobody can judge — the same instance while nothing it holds changed, and no
    /// pattern made to find that out.</summary>
    private MatchMarks<TRow>? MatchMarksNow()
    {
        ForgetCurrentMatchOffFocus();
        if (!_findOpen || _findText.Length == 0 || FindMatchRule() is not { } rule)
            return _matchMarks = null;
        return _matchMarks is { } held && held.Same(rule, _findText, _findMatchCase, _findWholeCell)
            ? held
            : _matchMarks = new MatchMarks<TRow>(rule, new GridFindPattern(_findText, _findMatchCase, _findWholeCell));
    }

    /// <summary>A found step's cell becomes the current match (ADR-0332 §4).</summary>
    private void TakeCurrentMatch(CellPosition found)
    {
        _findCurrent = found;
        _findCurrentVersion = _sequenceVersion;
    }

    /// <summary>
    /// Forgets the find panel's current match (ADR-0332 §4), as the grid's own Replace All does: after a
    /// write the grid did not make, no match is current, so the next Replace finds one before it writes
    /// rather than writing the cell the last step found. For a Consumer that performs a Replace All itself,
    /// outside the Replace Request — ExSheet's over the Workbook (ADR-0332 §5). Call it on the renderer's
    /// context, from the handler that writes; with no current match it does nothing.
    /// </summary>
    public void ForgetCurrentMatch()
    {
        if (_findCurrent is null)
            return;
        _findCurrent = null;
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>The current match is the Focus a found step moved to only while the Focus stays
    /// there, in the order it was found in: once either moves it is forgotten, not merely hidden.
    /// Asked wherever the Selection is replaced, and again before each render.</summary>
    private void ForgetCurrentMatchOffFocus()
    {
        if (_findCurrent is not { } current)
            return;
        var selection = _selection.Selection;
        if (selection.IsEmpty || selection.Focus != current || _findCurrentVersion != _sequenceVersion)
            _findCurrent = null;
    }

    /// <summary>The column of the current match in <paramref name="row"/>, or -1: every row but the
    /// one the Focus is in is handed -1, so none of them renders when a step moves on.</summary>
    private int CurrentMatchAt(int row, MatchMarks<TRow>? marks)
        => marks is not null && _findCurrent is { } current && current.Row == row ? current.Column : -1;

    /// <summary>A substituted panel's change to what the panel holds (ADR-0055): the grid renders
    /// for it where the marks may move, as it does for the built-in panel's own fields.</summary>
    private void FindHeldChanged()
    {
        if (_findOpen && FindMatchRule() is not null)
            StateHasChanged();
    }
}
