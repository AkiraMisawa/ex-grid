namespace ExSheet.Engine;

/// <summary>
/// What one cell holds and shows, compared before and after an operation. Its format and
/// alignment are the cell's own; <see langword="null"/> takes the row's or the column's (ADR-0047).
/// </summary>
internal readonly record struct CellState(Entry? Entry, Value? Value, NumberFormat? Format, HorizontalAlignment? Alignment)
{
    public static CellState Blank { get; } = new(null, null, null, null);

    /// <summary>The same cell as recorded: Entry and formatting, the Value left out.</summary>
    public CellState Recorded => this with { Value = null };

    public bool SameRecord(CellState other) =>
        Equals(Entry, other.Entry) && Equals(Format, other.Format) && Alignment == other.Alignment;
}

public sealed partial class Sheet
{
    /// <summary>Every held cell's state, keyed by address.</summary>
    private Dictionary<CellAddress, CellState> Snapshot() =>
        _cells.Values.ToDictionary(c => c.Address, c => new CellState(c.Entry, c.Value, c.Format, c.Alignment));

    private CellState StateOf(CellAddress address) =>
        _cells.TryGetValue(address, out var c) ? new CellState(c.Entry, c.Value, c.Format, c.Alignment) : CellState.Blank;

    /// <summary>
    /// The cell as a copy or a fill carries it: its Entry, and the format and alignment it shows
    /// from whichever level sets them, as Excel's paste brings the source's formatting (ADR-0047).
    /// </summary>
    private CellState ShownState(CellAddress address) =>
        StateOf(address).Recorded with { Format = GetFormat(address), Alignment = GetAlignment(address) };

    /// <summary>
    /// States about to be written whole, each cell's format and alignment kept only where they
    /// differ from what its row or column gives it, so a pasted cell records nothing it would take
    /// anyway.
    /// </summary>
    internal List<(CellAddress Address, CellState State)> Settle(IEnumerable<(CellAddress Address, CellState State)> states) =>
        [.. states.Select(p => (p.Address, p.State with
        {
            Format = p.State.Format is { } f && f.Equals(InheritedFormat(p.Address)) ? null : p.State.Format,
            Alignment = p.State.Alignment is { } a && a == InheritedAlignment(p.Address) ? null : p.State.Alignment,
        }))];

    /// <summary>The format and alignment every held cell shows, keyed by address.</summary>
    private Dictionary<CellAddress, (NumberFormat Format, HorizontalAlignment Alignment)> ShownSnapshot() =>
        _cells.Keys.ToDictionary(a => a, a => (GetFormat(a), GetAlignment(a)));

    /// <summary>
    /// The change between <paramref name="before"/> and now, address by address: a Value that
    /// differs is a value change, and anything that differs puts its row in <see cref="SheetChange.Rows"/>.
    /// </summary>
    private SheetChange Diff(
        Dictionary<CellAddress, CellState> before,
        IEnumerable<CellAddress> recalculated,
        Dictionary<CellAddress, (NumberFormat Format, HorizontalAlignment Alignment)>? shownBefore = null)
    {
        var valueChanges = new List<CellAddress>();
        var rows = new SortedSet<int>();
        void Compare(CellAddress address, CellState old)
        {
            var now = StateOf(address);
            if (!Nullable.Equals(old.Value, now.Value)) valueChanges.Add(address);
            var shown = shownBefore is null
                || (shownBefore.TryGetValue(address, out var was) ? was : (NumberFormat.General, HorizontalAlignment.General)) == (GetFormat(address), GetAlignment(address));
            if (!Nullable.Equals(old.Value, now.Value) || !old.SameRecord(now) || !shown) rows.Add(address.Row);
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
