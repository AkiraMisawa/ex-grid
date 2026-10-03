using ExGrid.Selection;

namespace ExGrid.Finding;

/// <summary>
/// The reference implementation of a Find step (ADR-0055), which
/// <see cref="InMemoryGridSource{TRow}"/> answers with and a remote Source is held to — as
/// <see cref="GridQueryEngine"/> is for filtering and sorting (ADR-0023).
///
/// <para>What is matched is the <b>displayed text</b>: the column's format applied, the text a
/// user reads and so the only form they can type. Compared <c>OrdinalIgnoreCase</c> unless
/// <see cref="GridFindRequest.MatchCase"/>, containment unless
/// <see cref="GridFindRequest.WholeCell"/>. The order is Excel's default, by rows: row by row,
/// and across a row in the request's column order, starting after
/// <see cref="GridFindRequest.From"/> and wrapping, so the cell it names is considered last
/// and a lone match finds itself.</para>
/// </summary>
public static class GridFind
{
    /// <summary>
    /// One step: the next match in <paramref name="rows"/> — the result in its current order.
    /// </summary>
    /// <param name="rows">Every row of the result, in the order the request was read in.</param>
    /// <param name="request">The step.</param>
    /// <param name="textOf">The displayed text of a column by name, or null for a column that
    /// has no text to search (an action column) or that this Source does not know.</param>
    public static GridFindResult Step<TRow>(
        IReadOnlyList<TRow> rows, GridFindRequest request, Func<string, Func<TRow, string>?> textOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(textOf);
        if (string.IsNullOrEmpty(request.Text) || rows.Count == 0 || request.Columns.Count == 0)
            return GridFindResult.NotFound;

        var columnCount = request.Columns.Count;
        var texts = new Func<TRow, string>?[columnCount];
        for (var i = 0; i < columnCount; i++)
            texts[i] = textOf(request.Columns[i]);

        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var total = (long)rows.Count * columnCount;
        // The ordinal of the cell From names, in row-major order; the walk starts one step
        // past it and ends on it. No From: the walk starts at the first cell (the last,
        // backward), which is the same as starting past a cell just before it.
        long start = request.From is { } from
            && from.Row >= 0 && from.Row < rows.Count && from.Column >= 0 && from.Column < columnCount
            ? (long)from.Row * columnCount + from.Column
            : request.Backward ? 0 : total - 1;
        var step = request.Backward ? -1L : 1L;

        for (long walked = 1; walked <= total; walked++)
        {
            var ordinal = ((start + step * walked) % total + total) % total;
            var row = (int)(ordinal / columnCount);
            var column = (int)(ordinal % columnCount);
            if (texts[column] is not { } text)
                continue;
            if (request.Scope is { } scope && !InScope(scope, row, column))
                continue;
            var shown = text(rows[row]);
            var matches = request.WholeCell
                ? string.Equals(shown, request.Text, comparison)
                : shown.Contains(request.Text, comparison);
            if (matches)
                return GridFindResult.Found(row, request.Columns[column]);
        }
        return GridFindResult.NotFound;
    }

    private static bool InScope(IReadOnlyList<SelectionRange> scope, int row, int column)
    {
        foreach (var range in scope)
        {
            if (range.Contains(new CellPosition(row, column)))
                return true;
        }
        return false;
    }
}
