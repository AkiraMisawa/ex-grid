namespace ExSheet.Engine;

/// <summary>
/// What one cell holds and shows, compared before and after an operation. The parts of its Cell
/// Format are the cell's own; <see langword="null"/> takes the row's or the column's (ADR-0047,
/// ADR-0063).
/// </summary>
internal readonly record struct CellState(
    Entry? Entry,
    Value? Value,
    NumberFormat? NumberFormat,
    HorizontalAlignment? Alignment,
    CellFont? Font,
    CellFill? Fill,
    CellBorders? Borders)
{
    public static CellState Blank { get; } = new(null, null, null, null, null, null, null);

    /// <summary>The same cell as recorded: Entry and Cell Format, the Value left out.</summary>
    public CellState Recorded => this with { Value = null };

    public bool SameRecord(CellState other) => Recorded == other.Recorded;

    /// <summary>The Cell Format the state shows, where it records nothing of a part, the part's default.</summary>
    public CellFormat CellFormat => new(
        NumberFormat ?? Engine.NumberFormat.General,
        Alignment ?? HorizontalAlignment.General,
        Font ?? CellFont.Default,
        Fill ?? CellFill.None,
        Borders ?? CellBorders.None);
}

public sealed partial class Sheet
{
    /// <summary>Every held cell's state, keyed by address.</summary>
    private Dictionary<CellAddress, CellState> Snapshot() => _cells.Values.ToDictionary(c => c.Address, State);

    private CellState StateOf(CellAddress address) => _cells.TryGetValue(address, out var c) ? State(c) : CellState.Blank;

    private static CellState State(Cell c) => new(c.Entry, c.Value, c.NumberFormat, c.Alignment, c.Font, c.Fill, c.Borders);

    /// <summary>
    /// The cell as a copy or a fill carries it: its Entry, and every part of the Cell Format it
    /// records, from whichever level records it, as Excel's paste brings the source's formatting
    /// (ADR-0047, ADR-0063). Its Borders are its own four sides, never a line it shows from the cell
    /// beside it, so a paste, Ctrl+D, Ctrl+R and the fill handle write the target's own sides and
    /// touch no neighbour (the twelfth Windows run, cases 1 to 5).
    /// </summary>
    private CellState CarriedState(CellAddress address)
    {
        var shown = OwnFormat(address);
        return StateOf(address).Recorded with
        {
            NumberFormat = shown.NumberFormat,
            Alignment = shown.Alignment,
            Font = shown.Font,
            Fill = shown.Fill,
            Borders = shown.Borders,
        };
    }

    /// <summary>
    /// States about to be written whole, each part of each cell's Cell Format kept only where it
    /// differs from what its row or column gives it, so a pasted cell records nothing it would take
    /// anyway.
    /// </summary>
    internal List<(CellAddress Address, CellState State)> Settle(IEnumerable<(CellAddress Address, CellState State)> states) =>
        [.. states.Select(p => (p.Address, Settled(p.State, Inherited(p.Address))))];

    private static CellState Settled(CellState state, CellFormat inherited) => state with
    {
        NumberFormat = state.NumberFormat is { } f && f.Equals(inherited.NumberFormat) ? null : state.NumberFormat,
        Alignment = state.Alignment is { } a && a == inherited.Alignment ? null : state.Alignment,
        Font = state.Font is { } font && font == inherited.Font ? null : state.Font,
        Fill = state.Fill is { } fill && fill == inherited.Fill ? null : state.Fill,
        Borders = state.Borders is { } borders && borders == inherited.Borders ? null : state.Borders,
    };

    /// <summary>The Cell Format every held cell shows, keyed by address.</summary>
    private Dictionary<CellAddress, CellFormat> ShownSnapshot() => _cells.Keys.ToDictionary(a => a, GetCellFormat);

    /// <summary>
    /// The change between <paramref name="before"/> and now, address by address: a Value that
    /// differs is a value change, and anything that differs puts its row in <see cref="SheetChange.Rows"/>.
    /// </summary>
    private SheetChange Diff(
        Dictionary<CellAddress, CellState> before,
        IEnumerable<CellAddress> recalculated,
        Dictionary<CellAddress, CellFormat>? shownBefore = null)
    {
        var valueChanges = new List<CellAddress>();
        var rows = new SortedSet<int>();
        void Compare(CellAddress address, CellState old)
        {
            var now = StateOf(address);
            if (!Nullable.Equals(old.Value, now.Value)) valueChanges.Add(address);
            var shown = shownBefore is null
                || (shownBefore.TryGetValue(address, out var was) ? was : CellFormat.Default) == GetCellFormat(address);
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
