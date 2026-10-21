using ExGrid.Selection;

namespace ExGrid.Finding;

/// <summary>
/// Which header a <see cref="FindHeader"/> is, whatever the column order or its text (ADR-0333): a
/// column header by its column's name, at <paramref name="Tier"/> 0; a Header Group label by its declared
/// top tier and the first member column it declares, which no other rectangle of that tier holds, as
/// rectangles may not overlap (ADR-0032). The current header is kept by this, so a reorder, or a header's
/// text changed, leaves it the same header, and the walk and the marks agree on which one it is.
/// </summary>
internal readonly record struct FindHeaderId(int Tier, string Column);

/// <summary>
/// One header Find can stop on (ADR-0333): a Header Group's label or a column's header, as the grid
/// shows it. <paramref name="HeaderRow"/> counts the header's rows from the top Header Group row, 0,
/// down to the column header row; <paramref name="FirstColumn"/> and <paramref name="LastColumn"/> are
/// the columns it stands over in the current order; <paramref name="Id"/> is which header it is.
/// </summary>
internal readonly record struct FindHeader(int HeaderRow, int FirstColumn, int LastColumn, string Text, FindHeaderId Id);

/// <summary>
/// The order a Find step walks once the headers are searched (ADR-0333 §3): the header rows first, top
/// to bottom and each left to right, then the data rows by rows, wrapping; backward the reverse. The
/// grid searches the headers itself and asks the Consumer only about rows, with the request ADR-0055
/// gave it, unchanged. This is the arithmetic of the two together, free of the component, so the
/// order can be pinned case by case.
/// </summary>
internal static class HeaderFindWalk
{
    /// <summary>
    /// The header a step lands on without asking the Consumer, or null when the Consumer is asked.
    /// From a header, the next matching header after it (before it, backward) is reached before any
    /// row. From nothing at all — no Focus, no header — a forward walk starts at the top, where the
    /// header rows are, so the first matching header is the answer; backward it starts at the end of
    /// the rows, and the Consumer is asked.
    /// </summary>
    /// <param name="matches">The matching headers' indices in walk order, ascending.</param>
    /// <param name="fromHeader">The current match's index when it is a header.</param>
    /// <param name="fromCell">Whether the step starts from a cell (the Focus).</param>
    /// <param name="backward">Whether the step runs backward.</param>
    public static int? BeforeAsking(IReadOnlyList<int> matches, int? fromHeader, bool fromCell, bool backward)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0)
            return null;
        if (fromHeader is { } from)
        {
            if (backward)
            {
                for (var i = matches.Count - 1; i >= 0; i--)
                {
                    if (matches[i] < from)
                        return matches[i];
                }
                return null;
            }
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i] > from)
                    return matches[i];
            }
            return null;
        }
        if (!fromCell && !backward)
            return matches[0];
        return null;
    }

    /// <summary>
    /// The header a step lands on once the Consumer has answered, or null when its answer stands. The
    /// Consumer was asked from the Focus (<paramref name="from"/>), or from the start of the rows (the
    /// end, backward) when the step began at a header or nowhere. Its answer passed the header rows
    /// when it wrapped — at or before <paramref name="from"/> forward, at or after it backward — or when
    /// nothing matched among the rows; then the first matching header (the last, backward) is reached
    /// first. Asked from the start, the answer wraps past nothing and stands whenever it was found.
    /// </summary>
    /// <param name="matches">The matching headers' indices in walk order, ascending.</param>
    /// <param name="from">The Focus the Consumer was asked from, or null when asked from the start.</param>
    /// <param name="answer">The cell the Consumer answered with, its column as an index of the request's
    /// columns, or null for "not found".</param>
    /// <param name="backward">Whether the step runs backward.</param>
    public static int? AfterAnswer(IReadOnlyList<int> matches, CellPosition? from, CellPosition? answer, bool backward)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0)
            return null;
        var wrapped = answer is not { } found
            || (from is { } start && (backward ? Compare(found, start) >= 0 : Compare(found, start) <= 0));
        if (!wrapped)
            return null;
        return backward ? matches[^1] : matches[0];
    }

    // Row-major order: by row, then across the row in the request's column order.
    private static int Compare(CellPosition a, CellPosition b)
        => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column);
}
