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
        // Asked once per row by whoever counts, so it must not walk every step: the key's
        // own step is looked up, and only the snapshots taken after it are tried.
        var index = Indexes.GetValue(this, static state => new Index(state.Steps));
        var (step, marked) = index.Keys.TryGetValue(key, out var own) ? own : (-1, false);
        for (var i = index.Snapshots.Count - 1; i >= 0 && index.Snapshots[i].Step > step; i--)
        {
            if (belongsTo(index.Snapshots[i].All.Snapshot))
                return index.Snapshots[i].All.Marked;
        }
        return marked;
    }

    // Built once per state and held beside it rather than in it: a field would take part
    // in the record's equality.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RowMarkState, Index> Indexes = new();

    private sealed class Index
    {
        public Index(IReadOnlyList<RowMarkStep> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                switch (steps[i])
                {
                    case RowMarkStep.OneKey one:
                        Keys[one.Key] = (i, one.Marked);
                        break;
                    case RowMarkStep.AllOf all:
                        Snapshots.Add((i, all));
                        break;
                }
            }
        }

        public Dictionary<object, (int Step, bool Marked)> Keys { get; } = [];

        public List<(int Step, RowMarkStep.AllOf All)> Snapshots { get; } = [];
    }
}
