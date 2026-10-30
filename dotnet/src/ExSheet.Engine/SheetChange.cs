namespace ExSheet.Engine;

/// <summary>
/// What one change to a Sheet did, reported after its recalculation has completed. Every Value it
/// names is already published, and all of them come from that one recalculation (ADR-0047): a
/// component hands back a new row instance for exactly the rows in <see cref="Rows"/>.
/// </summary>
public sealed class SheetChange
{
    internal SheetChange(
        IReadOnlyList<CellAddress> valueChanges, IReadOnlyList<CellAddress> recalculated, IReadOnlyList<int> rows,
        IReadOnlyList<int>? columns = null, bool reformatsUnnamedRows = false)
    {
        ValueChanges = valueChanges;
        Recalculated = recalculated;
        Rows = rows;
        Columns = columns ?? [];
        ReformatsUnnamedRows = reformatsUnnamedRows;
    }

    /// <summary>A change that changed nothing.</summary>
    internal static SheetChange None { get; } = new([], [], []);

    /// <summary>The cells whose Value changed (a cell becoming blank included), in row-major order.</summary>
    public IReadOnlyList<CellAddress> ValueChanges { get; }

    /// <summary>
    /// The Formula cells this change recomputed, in row-major order: the changed cells and the
    /// cells that depend on them, and no other (ADR-0047).
    /// </summary>
    public IReadOnlyList<CellAddress> Recalculated { get; }

    /// <summary>
    /// The rows whose painted content may differ — a Value, an Entry or a cell's formatting changed —
    /// in ascending order, each once.
    /// </summary>
    public IReadOnlyList<int> Rows { get; }

    /// <summary>
    /// The columns whose recorded width changed (ADR-0046) — set, cleared back to the default, or
    /// moved by an insertion or deletion — in ascending order, each once.
    /// </summary>
    public IReadOnlyList<int> Columns { get; }

    /// <summary>
    /// Whether the Cell Format shown in rows <see cref="Rows"/> does not name may have changed
    /// (ADR-0071). <see cref="Rows"/> names the rows of the cells a change wrote, and the row across
    /// each top or bottom side it changed (<see cref="Sheet.GetBorders"/>). Two kinds of change reach
    /// further, into cells that hold nothing:
    /// <list type="bullet">
    /// <item>a whole row's or a whole column's Cell Format set, cleared or moved, which every cell
    /// of that row or column shows;</item>
    /// <item>rows or columns inserted or deleted, which bring two cells' sides together on one
    /// edge, so the line shown on it can change from a cell that holds nothing.</item>
    /// </list>
    /// After either, a component that paints a Cell Format reads again what each row it paints
    /// shows, and repaints the rows where that differs.
    /// </summary>
    public bool ReformatsUnnamedRows { get; }

    /// <summary>This change, also reporting that it may have reformatted rows it does not name.</summary>
    internal SheetChange ReformattingUnnamedRows() =>
        ReformatsUnnamedRows ? this : new SheetChange(ValueChanges, Recalculated, Rows, Columns, reformatsUnnamedRows: true);

    /// <summary>Several changes made as one, such as the parts of one step.</summary>
    internal static SheetChange Merge(IEnumerable<SheetChange> changes)
    {
        var list = changes.ToList();
        var valueChanges = list.SelectMany(c => c.ValueChanges).Distinct().Order().ToList();
        var recalculated = list.SelectMany(c => c.Recalculated).Distinct().Order().ToList();
        var rows = list.SelectMany(c => c.Rows).Distinct().Order().ToList();
        var columns = list.SelectMany(c => c.Columns).Distinct().Order().ToList();
        var reformats = list.Any(c => c.ReformatsUnnamedRows);
        return valueChanges.Count == 0 && recalculated.Count == 0 && rows.Count == 0 && columns.Count == 0 && !reformats
            ? None
            : new SheetChange(valueChanges, recalculated, rows, columns, reformats);
    }
}
