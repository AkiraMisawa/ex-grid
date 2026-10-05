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
    /// <see cref="From{TRow}(IReadOnlyList{TRow})"/> for live data (ADR-0141): the rows are named
    /// by <paramref name="rowKey"/>, and the source takes Change Batches and whole new lists,
    /// gathered on <paramref name="clock"/> and paired by key. It names its rows to the grid by the
    /// key and vouches that no Window holds a row twice (ADR-0140). Filter and Sort mean exactly
    /// what they mean without a key (ADR-0023).
    /// </summary>
    /// <param name="rows">The rows, in base order.</param>
    /// <param name="rowKey">A row's Row Key: equal for every version of the same row, and only for
    /// it, and never null. A key that repeats among <paramref name="rows"/>, or a null one, is
    /// refused by name.</param>
    /// <param name="clock">The clock the changes are gathered on and the change times read from. A
    /// source cannot see the host's services, so null is <see cref="TimeProvider.System"/>: a host
    /// that registers a <see cref="TimeProvider"/> for the grid passes the same one here, and a test
    /// passes its own.</param>
    public static InMemoryGridSource<TRow> From<TRow>(
        IReadOnlyList<TRow> rows, Func<TRow, object> rowKey, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(rowKey);
        return new InMemoryGridSource<TRow>(rows, rowKey, clock ?? TimeProvider.System);
    }

    /// <summary>
    /// The result lives somewhere else: the source asks <paramref name="fetch"/> for one
    /// range at a time and holds the answer as the Window (ADR-0025). It fetches the
    /// first page itself, coalesces requests while scrolling, cancels what a newer
    /// question supersedes and discards an answer that arrives after it.
    /// </summary>
    /// <param name="fetch">Answers a <see cref="GridQuery"/> with a <see cref="GridPage{TRow}"/>.
    /// Its Filter and Sort semantics are expected to match <see cref="From{TRow}(IReadOnlyList{TRow})"/> (ADR-0023).</param>
    /// <param name="readAheadRows">Rows fetched either side of what the grid asked for, so
    /// ordinary scrolling does not ask again immediately. Read-ahead is a judgement about
    /// the Consumer's data, which is why it is a number here and not a guess in the grid
    /// (ADR-0001).</param>
    /// <param name="distinctValues">Answers the filter panel's value list (ADR-0009):
    /// a DISTINCT of one column with the handed Filter — the other columns' conditions,
    /// this one's already removed — applied. Null degrades every value list to
    /// TooMany, which is condition mode.</param>
    /// <param name="marks">What the source needs from the Consumer to keep Row Marks — a
    /// row's key, and the server's answers about snapshots and counts (ADR-0043). Null
    /// keeps none, and a Mark Column bound to this source is refused by name.</param>
    /// <param name="find">Answers a Find step (ADR-0055) against the result the handed Filter
    /// and Sorts produce — the ones in force — with the next matching position, as
    /// <see cref="Finding.GridFind.Step{TRow}"/> would over that result. Null reports that
    /// this source cannot search, and Ctrl+F is refused.</param>
    /// <param name="rowKey">A row's Row Key, for live data (ADR-0141): with one, the source can be
    /// told that the data moved on (<see cref="FetchingGridSource{TRow}.NotifyChanged"/>), pairs each
    /// answer's rows with the painted ones by it, marks the cells that changed, and refuses an answer
    /// that repeats a key. The Row Mark adapter's key is one (ADR-0140), and serves when this is
    /// left out; both given must be the same delegate. Null with no adapter takes no live data.</param>
    /// <param name="clock">The clock the notices are gathered on and the change times read from.
    /// A source cannot see the host's services, so null is <see cref="TimeProvider.System"/>: a host
    /// that registers a <see cref="TimeProvider"/> for the grid passes the same one here, and a test
    /// passes its own.</param>
    public static FetchingGridSource<TRow> Fetch<TRow>(
        Func<GridQuery, CancellationToken, ValueTask<GridPage<TRow>>> fetch,
        int readAheadRows = 60,
        Func<string, GridFilter?, CancellationToken, Task<Chrome.DistinctValues>>? distinctValues = null,
        Rows.RowMarkAdapter<TRow>? marks = null,
        Func<Finding.GridFindRequest, GridFilter?, IReadOnlyList<SortSpec>, CancellationToken, Task<Finding.GridFindResult>>? find = null,
        Func<TRow, object>? rowKey = null,
        TimeProvider? clock = null)
        => new(fetch, readAheadRows, distinctValues, marks, find, rowKey, clock);
}
