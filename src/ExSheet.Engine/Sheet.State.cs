namespace ExSheet.Engine;

/// <summary>What one cell holds and shows, compared before and after an operation.</summary>
internal readonly record struct CellState(Entry? Entry, Value? Value, NumberFormat Format, HorizontalAlignment Alignment)
{
    public static CellState Blank { get; } = new(null, null, NumberFormat.General, HorizontalAlignment.General);

    /// <summary>The same cell as recorded: Entry and formatting, the Value left out.</summary>
    public CellState Recorded => this with { Value = null };

    public bool SameRecord(CellState other) =>
        Equals(Entry, other.Entry) && Format.Equals(other.Format) && Alignment == other.Alignment;
}

public sealed partial class Sheet
{
    /// <summary>Every held cell's state, keyed by address.</summary>
    private Dictionary<CellAddress, CellState> Snapshot() =>
        _cells.Values.ToDictionary(c => c.Address, c => new CellState(c.Entry, c.Value, c.Format, c.Alignment));

    private CellState StateOf(CellAddress address) =>
        _cells.TryGetValue(address, out var c) ? new CellState(c.Entry, c.Value, c.Format, c.Alignment) : CellState.Blank;

    /// <summary>
    /// The change between <paramref name="before"/> and now, address by address: a Value that
    /// differs is a value change, and anything that differs puts its row in <see cref="SheetChange.Rows"/>.
    /// </summary>
    private SheetChange Diff(Dictionary<CellAddress, CellState> before, IEnumerable<CellAddress> recalculated)
    {
        var valueChanges = new List<CellAddress>();
        var rows = new SortedSet<int>();
        void Compare(CellAddress address, CellState old)
        {
            var now = StateOf(address);
            if (!Nullable.Equals(old.Value, now.Value)) valueChanges.Add(address);
            if (!Nullable.Equals(old.Value, now.Value) || !old.SameRecord(now)) rows.Add(address.Row);
        }
        foreach (var (address, old) in before) Compare(address, old);
        foreach (var address in _cells.Keys)
        {
            if (!before.ContainsKey(address)) Compare(address, CellState.Blank);
        }
        valueChanges.Sort();
        var recomputed = recalculated.Distinct().Order().ToList();
        return valueChanges.Count == 0 && rows.Count == 0 && recomputed.Count == 0 ? SheetChange.None : new SheetChange(valueChanges, recomputed, [.. rows]);
    }

    /// <summary>Forgets and rebuilds the dependency graph from every Formula held.</summary>
    private void RebuildDependencies()
    {
        _cellDependents.Clear();
        _areaPrecedents.Clear();
        _tableReaders.Clear();
        foreach (var cell in _cells.Values)
        {
            if (cell.Entry?.Parsed is { } parsed) Register(cell.Address, parsed);
        }
    }
}
