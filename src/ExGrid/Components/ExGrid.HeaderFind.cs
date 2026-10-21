using ExGrid.Columns;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// Find reaches the column headers (ADR-0333): the grid searches its visible column headers and Header
// Group labels itself, by the reference rule over the text it shows, and asks the Consumer only about
// rows, with ADR-0055's request unchanged. The header rows come before the data rows in the walk
// (HeaderFindWalk). A header a step finds is the current match: it wears the current Match Mark, is
// scrolled into view across, and the panel says it was found in a header; the Focus does not move, and
// the next step continues from it. Every header in view that matches wears the Match Mark. A selection
// as the scope searches no header, and Replace never writes one.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// Whether Find searches the column headers and Header Group labels (ADR-0333), on by default. While
    /// on, a Find step walks the header rows before the data rows — the top Header Group row first, down
    /// to the column header row, each left to right in the current column order — and wraps, forward and
    /// backward. A header that matches becomes the current match: it wears the current Match Mark, is
    /// scrolled into view across, and the panel says it was found in a header
    /// (<see cref="FindOutcome.FoundInHeader"/>); <b>the Focus does not move</b>, as a header is not a
    /// cell, and the next step continues from it. Every header in view that matches wears the Match Mark,
    /// judged by <see cref="GridFind.Matches(string, GridFindPattern)"/> over the header's text, whoever
    /// answers Find: the grid holds that text, so no Consumer's match function is involved. The Consumer's
    /// request is unchanged; the grid reads whether its answer wrapped.
    ///
    /// <para>Hidden columns' headers are not searched, nor is a hidden header (<see cref="HideHeader"/>),
    /// nor any header while a selection of more than one cell is the scope. Replace never writes a header:
    /// with one as the current match it finds the next match. Turn it off where the headers are addresses
    /// rather than content, as ExSheet's column letters are.</para>
    /// </summary>
    [Parameter] public bool FindsHeaders { get; set; } = true;

    // The header a step found, and the Selection it was found under: the current match while that
    // Selection stands, forgotten the moment it is replaced, so the next step starts from the Focus the
    // user moved to rather than from a header they have left.
    private FindHeader? _findCurrentHeader;
    private GridSelection? _findCurrentHeaderSelection;

    // The columns a header found is revealed across: its first and last.
    private (int First, int Last)? _revealHeaderAcross;

    // What the header band was last handed, kept while the search, the columns, the Header Groups and
    // the current header stand: the root renders on every scroll frame, and no header is judged again
    // for it.
    private HeaderMatchMarks? _headerMarks;

    /// <summary>The headers a step may stop on, in walk order (ADR-0333 §3): each Header Group row from
    /// the top, then the column header row, each left to right in the current column order. None while
    /// the headers are not searched or not shown.</summary>
    private FindHeader[] FindHeaders()
    {
        if (!FindsHeaders || HideHeader)
            return [];
        var groups = _headerLayout.Groups;
        var headers = new List<FindHeader>(Columns.Count + groups.Count);
        for (var headerRow = 0; headerRow < _headerLayout.TierCount; headerRow++)
        {
            var tier = _headerLayout.TierCount - headerRow;
            var start = headers.Count;
            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                if (group.TopTier == tier)
                    headers.Add(new FindHeader(headerRow, group.FirstColumn, group.FirstColumn + group.MemberCount - 1, group.Label, null));
            }
            // Declared in any order; walked left to right.
            headers.Sort(start, headers.Count - start, Comparer<FindHeader>.Create(static (a, b) => a.FirstColumn.CompareTo(b.FirstColumn)));
        }
        for (var c = 0; c < Columns.Count; c++)
            headers.Add(new FindHeader(_headerLayout.TierCount, c, c, Columns[c].Header, Columns[c].Name));
        return [.. headers];
    }

    /// <summary>The indices of the headers that match <paramref name="pattern"/>, ascending.</summary>
    private static int[] MatchingHeaders(FindHeader[] headers, GridFindPattern pattern)
    {
        var matches = new List<int>();
        for (var i = 0; i < headers.Length; i++)
        {
            if (GridFind.Matches(headers[i].Text, pattern))
                matches.Add(i);
        }
        return [.. matches];
    }

    /// <summary>A column's position among a request's columns, which it is known to be among.</summary>
    private static int RequestColumnIndex(IReadOnlyList<string> columns, string name)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i] == name)
                return i;
        }
        return -1;
    }

    /// <summary>The current header's index among <paramref name="headers"/>, or null: none is current,
    /// the Selection it was found under has been replaced, or it no longer stands where it stood.</summary>
    private int? CurrentHeaderIn(FindHeader[] headers)
    {
        ForgetCurrentHeaderOffSelection();
        if (_findCurrentHeader is not { } current)
            return null;
        var index = Array.IndexOf(headers, current);
        return index < 0 ? null : index;
    }

    /// <summary>The current header is forgotten once the Selection it was found under is replaced: the
    /// user has moved, and the next step starts from where they moved to.</summary>
    private void ForgetCurrentHeaderOffSelection()
    {
        if (_findCurrentHeader is not null && !ReferenceEquals(_findCurrentHeaderSelection, _selection.Selection))
        {
            _findCurrentHeader = null;
            _findCurrentHeaderSelection = null;
        }
    }

    /// <summary>No header is current any more: a new step, a write, or the panel opened again.</summary>
    private void ForgetCurrentHeader()
    {
        _findCurrentHeader = null;
        _findCurrentHeaderSelection = null;
    }

    /// <summary>
    /// A step's header becomes the current match (ADR-0333 §1): the current Match Mark on it, the
    /// columns under it scrolled into view across, the panel saying it was found in a header. The Focus
    /// and the Selection stay where they are.
    /// </summary>
    private void LandOnHeader(FindHeader header)
    {
        _findCurrent = null;
        _findCurrentHeader = header;
        _findCurrentHeaderSelection = _selection.Selection;
        _findOutcome = FindOutcome.FoundInHeader;
        _revealHeaderAcross = (header.FirstColumn, header.LastColumn);
        _revealColumnAcross = null;
        _revealTarget = null;
        _revealFocus = true;
        _revealRowStart = false;
        _revealStepRows = 0;
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>The scroll offset across that brings a header's columns into view: its last column, and
    /// then its first, so a header wider than the Viewport shows its start. A pinned header is in view
    /// already, and answers the offset the grid has.</summary>
    private double ScrollLeftToRevealHeader(int first, int last)
    {
        var geometry = _columnStyles.Geometry;
        var left = _scrollLeftPx;
        if (last < geometry.Count)
            left = geometry.ScrollLeftToReveal(last, left);
        if (first < geometry.Count)
            left = geometry.ScrollLeftToReveal(first, left);
        return left;
    }

    /// <summary>What the header band marks in this render (ADR-0333 §1), or null while the panel is
    /// closed, holds no text, or the headers are not searched — the same instance while nothing it
    /// holds changed.</summary>
    private HeaderMatchMarks? HeaderMarksNow()
    {
        ForgetCurrentHeaderOffSelection();
        if (!_findOpen || _findText.Length == 0 || !FindsHeaders || HideHeader)
            return _headerMarks = null;
        if (_headerMarks is { } held
            && held.Same(_findText, _findMatchCase, _findWholeCell, Columns, _headerLayout, _findCurrentHeader))
        {
            return held;
        }
        var pattern = new GridFindPattern(_findText, _findMatchCase, _findWholeCell);
        var leaves = new byte[Columns.Count];
        for (var c = 0; c < Columns.Count; c++)
        {
            if (GridFind.Matches(Columns[c].Header, pattern))
                leaves[c] = _findCurrentHeader is { Column: { } name } && name == Columns[c].Name ? HeaderMatchMarks.Current : HeaderMatchMarks.Match;
        }
        var groups = _headerLayout.Groups;
        var groupMarks = new byte[groups.Count];
        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (!GridFind.Matches(group.Label, pattern))
                continue;
            var header = new FindHeader(_headerLayout.TierCount - group.TopTier, group.FirstColumn, group.FirstColumn + group.MemberCount - 1, group.Label, null);
            groupMarks[g] = _findCurrentHeader == header ? HeaderMatchMarks.Current : HeaderMatchMarks.Match;
        }
        return _headerMarks = new HeaderMatchMarks(pattern, Columns, _headerLayout, _findCurrentHeader, leaves, groupMarks);
    }

    // A Header Group's class with its mark, interned: never composed in the render loop (ADR-0027 P5).
    private static readonly string[] GroupClasses =
        ["ex-header-group", "ex-header-group ex-match-mark", "ex-header-group ex-match-mark ex-match-mark-current"];

    private static string GroupClass(int group, HeaderMatchMarks? marks)
        => GroupClasses[marks?.GroupMark(group) ?? 0];

    private static int LeafMark(int column, HeaderMatchMarks? marks) => marks?.LeafMark(column) ?? 0;

    /// <summary>What the header band marks (ADR-0333 §1): for each column header and each Header Group,
    /// none, a match, or the current match.</summary>
    private sealed class HeaderMatchMarks(
        GridFindPattern pattern, IReadOnlyList<GridColumn<TRow>> columns, HeaderGroupLayout layout,
        FindHeader? current, byte[] leaves, byte[] groups)
    {
        public const byte Match = 1;
        public const byte Current = 2;

        public bool Same(string text, bool matchCase, bool wholeCell, IReadOnlyList<GridColumn<TRow>> nowColumns,
            HeaderGroupLayout nowLayout, FindHeader? nowCurrent)
            => ReferenceEquals(columns, nowColumns) && ReferenceEquals(layout, nowLayout) && current == nowCurrent
                && string.Equals(pattern.Text, text, StringComparison.Ordinal)
                && pattern.MatchCase == matchCase && pattern.WholeCell == wholeCell;

        public int LeafMark(int column) => column < leaves.Length ? leaves[column] : 0;

        public int GroupMark(int group) => group < groups.Length ? groups[group] : 0;
    }
}
