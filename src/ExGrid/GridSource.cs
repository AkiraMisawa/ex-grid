namespace ExGrid;

/// <summary>
/// The bundled convenience layer on top of the push interface (ADR-0001). Both entry
/// points drive the push form internally; there is only one behavioural path.
/// </summary>
public static class GridSource
{
    /// <summary>Everything is in hand: the Window is the whole filtered-and-sorted
    /// result, and this is the reference implementation of what Filter and Sort
    /// <em>mean</em> (ADR-0023).</summary>
    public static InMemoryGridSource<TRow> From<TRow>(IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new InMemoryGridSource<TRow>(rows);
    }

    /// <summary>
    /// The result lives somewhere else: the source asks <paramref name="fetch"/> for one
    /// range at a time and holds the answer as the Window (ADR-0025). It fetches the
    /// first page itself, coalesces requests while scrolling, cancels what a newer
    /// question supersedes and discards an answer that arrives after it.
    /// </summary>
    /// <param name="fetch">Answers a <see cref="GridQuery"/> with a <see cref="GridPage{TRow}"/>.
    /// Its Filter and Sort semantics are expected to match <see cref="From"/> (ADR-0023).</param>
    /// <param name="readAheadRows">Rows fetched either side of what the grid asked for, so
    /// ordinary scrolling does not ask again immediately. Read-ahead is a judgement about
    /// the Consumer's data, which is why it is a number here and not a guess in the grid
    /// (ADR-0001).</param>
    public static FetchingGridSource<TRow> Fetch<TRow>(
        Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
        int readAheadRows = 60)
        => new(fetch, readAheadRows);
}
