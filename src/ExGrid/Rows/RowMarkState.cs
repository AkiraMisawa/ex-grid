namespace ExGrid.Rows;

/// <summary>
/// "The result under <see cref="Filter"/> as it stood at <see cref="AsOf"/>" — what a
/// header press marks on a fetching Source (ADR-0043). The token is the server's: a
/// watermark, a timestamp, a version, whatever lets it say later which rows belonged.
/// </summary>
/// <param name="Filter">The Filter in force when the header was pressed.</param>
/// <param name="AsOf">The server's own marker for that moment.</param>
public sealed record RowMarkSnapshot(GridFilter? Filter, object AsOf);

/// <summary>One step of a <see cref="RowMarkState"/>: a later step covering a row
/// overrides every earlier one.</summary>
public abstract record RowMarkStep
{
    private RowMarkStep()
    {
    }

    /// <summary>Whether this step marks what it covers, or unmarks it.</summary>
    public abstract bool Marked { get; }

    /// <summary>One row, by the key the Consumer's adapter reads from it.</summary>
    /// <param name="Key">The row's key.</param>
    /// <param name="Marked">Marked or unmarked.</param>
    public sealed record OneKey(object Key, bool Marked) : RowMarkStep
    {
        /// <inheritdoc />
        public override bool Marked { get; } = Marked;
    }

    /// <summary>Every row of a snapshot of the result.</summary>
    /// <param name="Snapshot">The result as it stood at the press.</param>
    /// <param name="Marked">Marked or unmarked.</param>
    public sealed record AllOf(RowMarkSnapshot Snapshot, bool Marked) : RowMarkStep
    {
        /// <inheritdoc />
        public override bool Marked { get; } = Marked;
    }
}

/// <summary>
/// The Row Marks of a fetching Source as the steps that made them, oldest first
/// (ADR-0043): what a Consumer sends to its server to count them or to run an action over
/// them. Identities and snapshots, never positions — a position names another row after
/// a sort, and a key that no longer exists fails loudly instead.
/// </summary>
/// <param name="Steps">The steps, oldest first. Only a key's latest step is kept.</param>
public sealed record RowMarkState(IReadOnlyList<RowMarkStep> Steps)
{
    /// <summary>No marks.</summary>
    public static RowMarkState Empty { get; } = new([]);

    /// <summary>
    /// Whether the row with this key is marked: the latest step that covers it decides,
    /// and a row no step covers is unmarked. The one evaluation the Source, a server
    /// counting and an action resolving the marks all share, so the three cannot
    /// disagree about what is marked.
    /// </summary>
    /// <param name="key">The row's key.</param>
    /// <param name="belongsTo">Whether the row belonged to a snapshot.</param>
    public bool IsMarked(object key, Func<RowMarkSnapshot, bool> belongsTo)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(belongsTo);
        for (var i = Steps.Count - 1; i >= 0; i--)
        {
            switch (Steps[i])
            {
                case RowMarkStep.OneKey one when one.Key.Equals(key):
                    return one.Marked;
                case RowMarkStep.AllOf all when belongsTo(all.Snapshot):
                    return all.Marked;
            }
        }
        return false;
    }
}
