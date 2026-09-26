namespace ExGrid.Rows;

/// <summary>
/// What a fetching Source needs from the Consumer to keep Row Marks (ADR-0043). It
/// receives new row instances with every answer, so identity has to come from a key; and
/// only the server can say which rows a snapshot of the result held, or count marks
/// beyond the Window. A Mark Column over <c>GridSource.Fetch</c> without one is refused by
/// name rather than painted with counts the Source cannot know.
/// </summary>
public sealed class RowMarkAdapter<TRow>
{
    /// <summary>The four answers a fetching Source cannot give itself.</summary>
    /// <param name="key">A row's key: equal for every instance of the same row, and only
    /// for it.</param>
    /// <param name="openSnapshot">Asked when the header is pressed: the server's marker
    /// for "the result under this Filter, now".</param>
    /// <param name="belongsTo">Whether a row belonged to a snapshot — asked for painted
    /// rows, so it must answer from the row alone (a creation stamp against the marker,
    /// and the Filter), never with a round trip.</param>
    /// <param name="count">The counts for the header and the count display, answered by
    /// the server from the marks' steps and the Filter in force.</param>
    public RowMarkAdapter(
        Func<TRow, object> key,
        Func<GridFilter?, CancellationToken, ValueTask<object>> openSnapshot,
        Func<TRow, RowMarkSnapshot, bool> belongsTo,
        Func<RowMarkState, GridFilter?, CancellationToken, ValueTask<RowMarkCounts>> count)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(openSnapshot);
        ArgumentNullException.ThrowIfNull(belongsTo);
        ArgumentNullException.ThrowIfNull(count);
        Key = key;
        OpenSnapshot = openSnapshot;
        BelongsTo = belongsTo;
        Count = count;
    }

    /// <summary>A row's key.</summary>
    public Func<TRow, object> Key { get; }

    /// <summary>The server's marker for the result under a Filter, now.</summary>
    public Func<GridFilter?, CancellationToken, ValueTask<object>> OpenSnapshot { get; }

    /// <summary>Whether a row belonged to a snapshot, from the row alone.</summary>
    public Func<TRow, RowMarkSnapshot, bool> BelongsTo { get; }

    /// <summary>The counts, from the marks' steps and the Filter in force.</summary>
    public Func<RowMarkState, GridFilter?, CancellationToken, ValueTask<RowMarkCounts>> Count { get; }
}
