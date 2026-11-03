namespace ExGrid.Finding;

/// <summary>
/// The reference Find All (ADR-0470), which <see cref="InMemoryGridSource{TRow}"/> answers with and a remote source is
/// held to, as <see cref="GridFind"/> is for a step: every cell a step would stop on, in the order a forward walk from
/// the start meets them, by rows and across a row in the request's column order, a page at a time with the exact
/// count. The grid lists the headers it searches itself, before the rows (ADR-0333), so the source is asked only
/// about rows.
/// </summary>
public static class GridFindAll
{
    /// <summary>Every match of a Find All over <paramref name="rows"/>, found once and read a page at a time.</summary>
    /// <param name="rows">Every row of the result, in the order the request was read in.</param>
    /// <param name="request">The list asked; its range is not read here.</param>
    /// <param name="textOf">The displayed text of a column by name, or null for a column with no text to search or one
    /// this source does not know, which is skipped, as a step skips it.</param>
    /// <param name="rowLabel">What the list calls each row, where the Consumer names its rows; null names them by
    /// position.</param>
    public static GridFindAllMatches<TRow> Matches<TRow>(
        IReadOnlyList<TRow> rows, GridFindAllRequest request, Func<string, Func<TRow, string>?> textOf, Func<TRow, string>? rowLabel = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(textOf);
        var columns = request.Columns.ToArray();
        var texts = new Func<TRow, string>?[columns.Length];
        for (var i = 0; i < columns.Length; i++)
            texts[i] = textOf(columns[i]);
        var ordinals = new List<long>();
        if (!string.IsNullOrEmpty(request.Text))
        {
            var pattern = request.Pattern;
            for (var row = 0; row < rows.Count; row++)
            {
                for (var column = 0; column < columns.Length; column++)
                {
                    if (texts[column] is not { } text)
                        continue;
                    if (request.Scope is { } scope && !GridFind.InScope(scope, row, column))
                        continue;
                    if (GridFind.Matches(text(rows[row]), pattern))
                        ordinals.Add((long)row * columns.Length + column);
                }
            }
        }
        return new(rows, columns, texts, ordinals, rowLabel);
    }

    /// <summary>One page of the reference Find All over <paramref name="rows"/>, read under
    /// <paramref name="rowSequenceVersion"/>: the whole list walked and the range asked read from it. A source that
    /// answers many pages of one list keeps a <see cref="GridFindAllMatches{TRow}"/> instead.</summary>
    /// <param name="rows">Every row of the result, in the order the request was read in.</param>
    /// <param name="rowSequenceVersion">The version the rows were read under.</param>
    /// <param name="request">The list and the range asked.</param>
    /// <param name="textOf">The displayed text of a column by name, as for <see cref="Matches{TRow}"/>.</param>
    public static GridFindAllPage Page<TRow>(
        IReadOnlyList<TRow> rows, int rowSequenceVersion, GridFindAllRequest request, Func<string, Func<TRow, string>?> textOf)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Matches(rows, request, textOf).Page(request.Start, request.Count, rowSequenceVersion);
    }
}

/// <summary>Every match of one Find All over one set of rows (ADR-0470 §2), each held as its cell's ordinal in
/// row-major order, read a page at a time.</summary>
/// <typeparam name="TRow">The row type.</typeparam>
public sealed class GridFindAllMatches<TRow>
{
    private readonly IReadOnlyList<TRow> _rows;
    private readonly string[] _columns;
    private readonly Func<TRow, string>?[] _texts;
    private readonly List<long> _ordinals;
    private readonly Func<TRow, string>? _rowLabel;

    internal GridFindAllMatches(IReadOnlyList<TRow> rows, string[] columns, Func<TRow, string>?[] texts, List<long> ordinals, Func<TRow, string>? rowLabel)
        => (_rows, _columns, _texts, _ordinals, _rowLabel) = (rows, columns, texts, ordinals, rowLabel);

    /// <summary>How many cells match.</summary>
    public int Total => _ordinals.Count;

    /// <summary>The entries from <paramref name="start"/>, <paramref name="count"/> of them or every one left, under
    /// <paramref name="rowSequenceVersion"/>. A range past the end holds none; a start below 0 or a count below 1 is
    /// refused.</summary>
    /// <param name="start">The first entry wanted.</param>
    /// <param name="count">How many are wanted.</param>
    /// <param name="rowSequenceVersion">The version the rows were read under.</param>
    public GridFindAllPage Page(int start, int count, int rowSequenceVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var matches = new List<GridFindAllMatch>();
        for (var at = start; at < _ordinals.Count && at < (long)start + count; at++)
        {
            var row = (int)(_ordinals[at] / _columns.Length);
            var column = (int)(_ordinals[at] % _columns.Length);
            var text = _texts[column] ?? throw new InvalidOperationException("Internal: a Find All match in a column with no text.");
            var label = _rowLabel is null ? null : _rowLabel(_rows[row]) ?? throw new InvalidOperationException(
                "The Find All row label answered null: it answers text for every row (ADR-0470 §3).");
            matches.Add(new GridFindAllMatch(row, _columns[column], text(_rows[row]), label));
        }
        return GridFindAllPage.Of(Total, matches, rowSequenceVersion);
    }
}
