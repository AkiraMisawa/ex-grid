namespace ExGrid;

/// <summary>
/// One answer to a <see cref="GridQuery"/>: the rows, the position they start at, and
/// how many rows the whole filtered result holds (ADR-0001). The position is part of the
/// answer rather than assumed from the question, because a server is free to answer with
/// less than it was asked for — a shrinking result, a clamp at the end — and every offset
/// on screen depends on knowing which positions these rows hold.
///
/// <para>A page that contradicts itself is refused here, at the one construction site: a
/// slice claiming rows past its own total would be painted at a position nothing else
/// agrees with, and the grid would be quietly showing the wrong rows under the right
/// scrollbar.</para>
/// </summary>
public sealed record GridPage<TRow>
{
    public GridPage(IReadOnlyList<TRow> rows, int start, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        if (start + rows.Count > totalCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows.Count,
                $"A page of {rows.Count} rows at {start} claims rows up to {start + rows.Count - 1}, " +
                $"past a TotalCount of {totalCount}.");
        }

        Rows = rows;
        Start = start;
        TotalCount = totalCount;
    }

    public IReadOnlyList<TRow> Rows { get; }

    /// <summary>The position of <see cref="Rows"/>[0] in the whole filtered result.</summary>
    public int Start { get; }

    /// <summary>Rows after filtering — what the scrollbar and the pager are made of.</summary>
    public int TotalCount { get; }

    /// <summary>The empty answer: a query that matched nothing.</summary>
    public static GridPage<TRow> Empty { get; } = new([], 0, 0);
}
