namespace ExGrid.Finding;

/// <summary>
/// The reference implementation of Replace (ADR-0332 §2 and §3), which
/// <see cref="InMemoryGridSource{TRow}.Replace"/> answers with and a remote Source is held to, as
/// <see cref="GridFind"/> is for Find.
///
/// <para><b>What is matched is the cell's entry</b> — the text the Cell Editor would open with — by
/// Find's rule (<see cref="GridFind.Matches(string, GridFindPattern)"/>): <c>OrdinalIgnoreCase</c>
/// unless Match case, containment unless Whole cell. A match is replaced as Excel's Replace does:
/// every occurrence of the text in the entry, compared the same way; with Whole cell, the entry
/// whole.</para>
///
/// <para><b>Replace All is all or nothing, and uncapped</b>: every match in the scope is planned
/// first; if any cannot be written — its column is not Editable, or the Consumer's write refuses the
/// replaced text — nothing is written, and every such cell is named. Otherwise every match is
/// written, and the count is the number of cells.</para>
/// </summary>
public static class GridReplace
{
    /// <summary>
    /// The reference rule for one cell (ADR-0332 §2): <paramref name="entry"/> with
    /// <paramref name="pattern"/> replaced by <paramref name="replacement"/>, or null where the entry
    /// does not match. Without Whole cell, every occurrence is replaced, compared as the pattern
    /// compares (<c>OrdinalIgnoreCase</c> unless Match case); with it, an entry equal to the text is
    /// replaced whole. An empty pattern matches nothing.
    /// </summary>
    /// <param name="entry">The cell's entry: the text the Cell Editor would open with.</param>
    /// <param name="pattern">What the find panel holds.</param>
    /// <param name="replacement">What the panel's Replace field holds; may be empty.</param>
    public static string? Replaced(string entry, GridFindPattern pattern, string replacement)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        if (!GridFind.Matches(entry, pattern))
            return null;
        if (pattern.WholeCell)
            return replacement;
        var comparison = pattern.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return entry.Replace(pattern.Text, replacement, comparison);
    }

    /// <summary>
    /// One Replace All over <paramref name="rows"/> — the result, in the order the request was read
    /// in — all or nothing. Read under another <paramref name="rowSequenceVersion"/> than the
    /// request's, it plans nothing: the answer carries that version, and the grid refuses it as the
    /// order having moved. Otherwise each row is written through <paramref name="write"/>, one
    /// matching cell after the next, left to right in the request's column order, each write on the
    /// instance the previous one returned; the answer's <see cref="GridReplaceAnswer{TRow}.Changes"/>
    /// hold each rewritten row once, for the Consumer to apply as one step — the caller applies them,
    /// this only plans. A cell the scope covers twice counts once.
    /// </summary>
    /// <param name="rows">Every row of the result, in the order the rows are held now.</param>
    /// <param name="rowSequenceVersion">The Row Sequence Version <paramref name="rows"/> are in.</param>
    /// <param name="request">The Replace All.</param>
    /// <param name="entryOf">The entry of a column by name — the text the Cell Editor opens with — or
    /// null for a column that has no text to search (an Action column) or that is not known.</param>
    /// <param name="write">Writes one cell's replaced entry: the row as written so far, the column's
    /// name and the new text. It is asked only of matches in Editable columns, and must only plan:
    /// it is asked of every such match even once one has refused, so the refusal names them all, and
    /// nothing it returns is applied then.</param>
    /// <exception cref="InvalidOperationException"><paramref name="write"/> returned the instance it
    /// was handed: an in-place rewrite does not reach the screen (ADR-0003).</exception>
    public static GridReplaceAnswer<TRow> Plan<TRow>(
        IReadOnlyList<TRow> rows,
        int rowSequenceVersion,
        GridReplaceRequest request,
        Func<string, Func<TRow, string>?> entryOf,
        Func<TRow, string, string, GridReplaceWrite<TRow>> write)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entryOf);
        ArgumentNullException.ThrowIfNull(write);
        if (request.RowSequenceVersion != rowSequenceVersion
            || string.IsNullOrEmpty(request.Text) || rows.Count == 0 || request.Columns.Count == 0)
        {
            return new(GridReplaceResult.Replaced(0, rowSequenceVersion), []);
        }

        var pattern = request.Pattern;
        var columnCount = request.Columns.Count;
        var entries = new Func<TRow, string>?[columnCount];
        var editable = new bool[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            entries[i] = entryOf(request.Columns[i]);
            editable[i] = request.EditableColumns.Contains(request.Columns[i]);
        }

        var refused = new List<GridReplaceRefusedCell>();
        var changes = new List<GridReplacedRow<TRow>>();
        var count = 0;
        for (var row = 0; row < rows.Count; row++)
        {
            var before = rows[row];
            var current = before;
            for (var column = 0; column < columnCount; column++)
            {
                if (entries[column] is not { } entry)
                    continue;
                if (request.Scope is { } scope && !GridFind.InScope(scope, row, column))
                    continue;
                // Judged on the row as read, whatever an earlier cell's write made of it.
                if (Replaced(entry(before), pattern, request.Replacement) is not { } text)
                    continue;
                var name = request.Columns[column];
                if (!editable[column])
                {
                    refused.Add(new GridReplaceRefusedCell(row, name, null));
                    continue;
                }
                // Asked even once a match has refused, so the refusal names every cell that cannot
                // be written; nothing it returns is applied then.
                var written = write(current, name, text);
                if (written.Refusal is { } why)
                {
                    refused.Add(new GridReplaceRefusedCell(row, name, why));
                    continue;
                }
                var next = written.Row!;
                if (ReferenceEquals(next, current))
                {
                    throw new InvalidOperationException(
                        $"The write of row {row}, column '{name}' returned the instance it was handed. An in-place " +
                        "rewrite does not reach the screen — return a new instance; identity is the change signal (ADR-0003).");
                }
                current = next;
                count++;
            }
            if (refused.Count == 0 && !ReferenceEquals(current, before))
                changes.Add(new GridReplacedRow<TRow>(before, current));
        }
        return refused.Count > 0
            ? new(GridReplaceResult.Refused(refused, rowSequenceVersion), [])
            : new(GridReplaceResult.Replaced(count, rowSequenceVersion), changes);
    }
}
